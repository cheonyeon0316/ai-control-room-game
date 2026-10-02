using UnityEngine;
using UnityEngine.AI;

namespace ControlRoom
{
    /// <summary>Suspends native navigation immediately without discarding its current path.</summary>
    internal sealed class NavMeshPauseState
    {
        public bool IsPaused { get; private set; }
        private bool captured, wasStopped, updatedPosition, updatedRotation;

        public void SetPaused(NavMeshAgent agent, bool paused)
        {
            if (paused)
            {
                IsPaused = true;
                Hold(agent);
                return;
            }
            if (!IsPaused) return;
            IsPaused = false;
            if (captured && Ready(agent))
            {
                // Preserve an explicit Warp performed while paused, and prevent a stale simulation position jump.
                agent.nextPosition = agent.transform.position;
                agent.velocity = Vector3.zero;
                agent.updatePosition = updatedPosition;
                agent.updateRotation = updatedRotation;
                agent.isStopped = wasStopped;
            }
            captured = false;
        }

        public void Hold(NavMeshAgent agent)
        {
            if (!IsPaused || !Ready(agent)) return;
            if (!captured)
            {
                wasStopped = agent.isStopped;
                updatedPosition = agent.updatePosition;
                updatedRotation = agent.updateRotation;
                agent.nextPosition = agent.transform.position;
                captured = true;
            }
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        public void KeepStoppedOnResume() { if (captured) wasStopped = true; }
        private static bool Ready(NavMeshAgent agent) => agent != null && agent.enabled && agent.isOnNavMesh;
    }
}
