using System;
using UnityEngine;
using UnityEngine.AI;

namespace ControlRoom
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GuardController : MonoBehaviour
    {
        public Action OnCaught;
        public Func<bool> CanAct;
        public FieldAgent Agent;
        public RiskLevel Risk = RiskLevel.LOW;
        public Transform Visual;
        public string CurrentVisualPose { get; private set; } = "Idle";
        public float DetectionDistance = 6.8f;
        public float ViewAngle = 105f;
        public NavMeshAgent Navigation => navigation;
        private NavMeshAgent navigation;
        private MissionWorld world;
        private Vector3[] patrol;
        private int patrolIndex;
        private float dwell;
        private float pose;
        private bool caught;
        private ProceduralCharacterPose characterPose;
        private readonly NavMeshPauseState navigationPause = new NavMeshPauseState();

        public void SetPaused(bool paused) => navigationPause.SetPaused(navigation, paused);

        public void Initialize(MissionWorld missionWorld)
        {
            world = missionWorld;
            if (Visual != null) characterPose = new ProceduralCharacterPose(Visual);
            navigation = GetComponent<NavMeshAgent>();
            navigation.radius = .34f;
            navigation.height = 1.9f;
            navigation.speed = 2.0f;
            navigation.acceleration = 9;
            navigation.angularSpeed = 260;
            navigation.stoppingDistance = .14f;
            patrol = new[] { new Vector3(14, 0, 12.6f), new Vector3(14, 0, 14.4f), new Vector3(0, 0, 14.4f), new Vector3(0, 0, 12.6f) };
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas)) navigation.Warp(start.position);
            else Debug.LogError("Guard has no valid runtime NavMesh start.");
        }

        public bool DetectAgent()
        {
            if (caught || Agent == null || Agent.State == AgentState.Hidden || Agent.State == AgentState.Caught || (CanAct != null && !CanAct())) return false;
            Vector3 toAgent = Agent.transform.position - transform.position;
            toAgent.y = 0;
            float distance = toAgent.magnitude;
            float radius = DetectionDistance + (Risk >= RiskLevel.HIGH ? 1.2f : 0);
            if (distance > radius || (distance > .1f && Vector3.Angle(transform.forward, toAgent) > ViewAngle * .5f)) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 target = Agent.transform.position + Vector3.up * 1.15f;
            Physics.SyncTransforms();
            return !Physics.Raycast(origin, (target - origin).normalized, Vector3.Distance(origin, target),
                1 << MissionWorld.ObstacleLayer, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Conservative safety check used by guard-clear conditions and cautious field movement.</summary>
        public bool GuardClearFor(Vector3 destination, FieldAgent agent = null)
        {
            Physics.SyncTransforms();
            FieldAgent field = agent ?? Agent;
            float clearance = DetectionDistance + 1.8f;
            if (Risk >= RiskLevel.HIGH) clearance += 1.2f;
            if (VisibleThreatAt(destination, clearance)) return false;
            if (field == null) return true;
            if (VisibleThreatAt(field.transform.position, clearance)) return false;
            if (field.Navigation != null && field.Navigation.isOnNavMesh)
            {
                var route = new NavMeshPath();
                if (field.Navigation.CalculatePath(destination, route))
                    foreach (Vector3 corner in route.corners) if (VisibleThreatAt(corner, clearance)) return false;
            }
            return true;
        }

        public bool GuardClearFor(FieldAgent agent) => agent != null && GuardClearFor(agent.transform.position, agent);

        private bool VisibleThreatAt(Vector3 point, float clearance)
        {
            if (Vector3.Distance(transform.position, point) > clearance) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 toPoint = point + Vector3.up * 1.15f - origin;
            return !Physics.Raycast(origin, toPoint.normalized, toPoint.magnitude,
                1 << MissionWorld.ObstacleLayer, QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            if (navigation == null || !navigation.isOnNavMesh) return;
            if (navigationPause.IsPaused)
            {
                navigationPause.Hold(navigation);
                return;
            }
            if (caught || (CanAct != null && !CanAct()))
            { navigation.isStopped = true; return; }
            navigation.isStopped = false;
            navigation.speed = Risk >= RiskLevel.HIGH ? 3.15f : Risk == RiskLevel.MEDIUM ? 2.45f : 2;
            // Compromised security expands patrol into the main hall after its doorway is physically opened.
            if (Risk == RiskLevel.COMPROMISED && world.Objects["STORAGE_DOOR"].IsOpen)
            {
                Vector3 target = patrolIndex % 2 == 0 ? world.Locations["MAIN_HALL"] : world.Locations["STORAGE"];
                if (!navigation.hasPath) navigation.SetDestination(target);
                if (!navigation.pathPending && navigation.remainingDistance <= .25f) { patrolIndex++; navigation.ResetPath(); }
            }
            else
            {
                if (!navigation.hasPath && dwell <= 0) navigation.SetDestination(patrol[patrolIndex % patrol.Length]);
                if (!navigation.pathPending && navigation.hasPath && navigation.remainingDistance <= .3f)
                { navigation.ResetPath(); patrolIndex = (patrolIndex + 1) % patrol.Length; dwell = .6f; }
                dwell = Mathf.Max(0, dwell - Time.deltaTime);
            }
            if (DetectAgent())
            {
                caught = true;
                navigation.ResetPath();
                navigation.isStopped = true;
                Agent.Catch();
                OnCaught?.Invoke();
            }
            Animate();
        }

        private void Animate()
        {
            if (Visual == null) return;
            if (characterPose == null) characterPose = new ProceduralCharacterPose(Visual);
            float speed = navigation.velocity.magnitude;
            bool running = speed > 2.8f;
            bool moving = speed > .12f;
            CurrentVisualPose = moving ? running ? "Run" : "Walk" : "Idle";
            pose += Time.deltaTime * (running ? 13f : moving ? 8.5f : 2f);
            float cycle = Mathf.Sin(pose);
            float amplitude = moving ? (running ? 35f : 23f) * Mathf.Clamp01(speed / 1.8f) : 0;
            Vector3 leftArm = new Vector3(-cycle * amplitude * .78f, 0, -5);
            Vector3 rightArm = new Vector3(cycle * amplitude * .78f, 0, 5);
            if (!moving) { leftArm.x = 0; rightArm.x = 0; }
            float bob = moving ? Mathf.Abs(cycle) * (running ? .05f : .027f) : cycle * .007f;
            characterPose.Apply(Vector3.up * bob, new Vector3(running ? 7 : 0, 0, 0), 1,
                leftArm, rightArm, new Vector3(cycle * amplitude, 0, 0), new Vector3(-cycle * amplitude, 0, 0),
                1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
