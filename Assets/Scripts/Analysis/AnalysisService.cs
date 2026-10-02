using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ControlRoom
{
    [Serializable]
    public sealed class DataSourceDocument
    {
        public string Id, Title, Content, Timestamp;
        public Reliability Reliability;
        public List<AnalysisRow> Rows = new List<AnalysisRow>();
        public DataSourceDocument(string id, string title, string content, string timestamp, Reliability reliability)
        { Id = id; Title = title; Content = content; Timestamp = timestamp; Reliability = reliability; }
    }

    /// <summary>Searches the authored archive only. It has no reference to world objects or Unity state.</summary>
    public sealed class AnalysisService
    {
        public const string MissionDate = "2026-10-02";
        private readonly List<DataSourceDocument> sources = new List<DataSourceDocument>();
        private readonly List<EvidenceRecord> authoredEvidence = new List<EvidenceRecord>();
        private bool usbVerified;
        private bool lastQueryWasUsb;
        private AnalysisResult lastResult;
        public IReadOnlyList<DataSourceDocument> Sources => sources.AsReadOnly();
        public IReadOnlyList<EvidenceRecord> AuthoredEvidence => authoredEvidence.AsReadOnly();

        public AnalysisService() { CreateArchive(); }

        public void Reset() { usbVerified = false; lastQueryWasUsb = false; lastResult = null; }

        public AnalysisResult Query(string prompt, string outputFormat = "summary")
        {
            prompt = (prompt ?? "").Trim();
            if (prompt.Length == 0) return Remember(Unknown("분석할 대상이나 조건을 알려 주세요."), false);
            string format = SelectFormat(prompt, outputFormat);
            bool usb = Has(prompt, "USB", "저장장치", "저장 장치", "storage device");
            bool followUp = Has(prompt, "이 정보", "이 결론", "그 근거", "이 결과", "출처", "충돌", "교차 검증");
            bool verification = Has(prompt, "충돌", "모순", "교차", "검증", "최신", "이동했", "재배치", "relocation", "cross", "verify", "compare", "비교");
            bool provenance = Has(prompt, "근거", "출처", "작성", "날짜", "source", "timestamp", "며칠", "오래");
            bool documentRequest = Has(prompt, "이메일", "email", "문서", "보고서", "메모", "자료 목록", "자료 요약", "터미널", "삭제", "키워드");

            // An explicit archive/document scope takes precedence over a keyword's mission topic.
            // Seeing "USB" in an EMAIL_02 or keyword request must not redirect it to EMAIL_01.
            var specifiedSources = sources.Where(source => prompt.IndexOf(source.Id, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (specifiedSources.Count > 0)
            {
                if (!documentRequest && specifiedSources.All(source => source.Rows.Count > 0))
                    return Remember(SearchRows(prompt, format, specifiedSources), false);
                return Remember(SearchDocuments(prompt, format, specifiedSources), usb);
            }
            if (documentRequest) return Remember(SearchDocuments(prompt, format), usb);

            if (verification && (usb || (lastQueryWasUsb && followUp)))
                return Remember(VerifyUsb(format), true);
            if (provenance && (usb || (lastQueryWasUsb && followUp)))
                return Remember(UsbSource(format, Has(prompt, "서버", "SERVER") || !usbVerified), true);
            if (usb) return Remember(UsbLocation(format), true);
            if (Has(prompt, "비밀번호", "암호", "코드", "731", "password", "cabinet code"))
                return Remember(CodeResult(format), false);
            if (Has(prompt, "용의자", "협력자", "침입자", "유출자", "누가 유출", "suspect"))
                return Remember(SuspectResult(format), false);

            if (Has(prompt, "CCTV", "CAM", "카메라", "출입", "들어", "누가", "인물", "사람", "동선", "이동 경로", "기록", "로그", "김민수", "박서연", "이도현") || ExtractTimes(prompt).Count > 0)
                return Remember(SearchRows(prompt, format, null), false);
            if (provenance && lastResult != null && followUp)
            {
                var matches = sources.Where(source => lastResult.sourceId.Split(',').Select(part => part.Trim()).Contains(source.Id)).ToList();
                if (matches.Count > 0) return Remember(FormatDocuments(matches, "with_evidence"), false);
            }
            return Remember(Unknown("보유 자료에서 이 요청을 판단할 근거를 찾지 못했습니다. 대상 문서, 인물, 장소 또는 시간 조건을 지정해 주세요."), false);
        }

        private AnalysisResult Remember(AnalysisResult result, bool usbTopic)
        {
            lastResult = result;
            lastQueryWasUsb = usbTopic;
            return result;
        }

        private AnalysisResult UsbLocation(string format)
        {
            if (usbVerified) return VerifyUsb(format);
            var stale = FindEvidence("E_USB_LOCATION");
            var result = Result("기밀 USB는 SERVER_ROOM(서버실)에 보관된 것으로 추정됩니다. 현재 위치는 현장에서 확인하지 못했습니다.", "EMAIL_01", "2026-09-29 20:00", Reliability.LIKELY);
            result.evidence.Add(stale);
            if (format == "with_evidence") result.text += "\n근거: EMAIL_01의 보관 지시 1건. 작성 후 위치가 달라졌을 수 있습니다.";
            return result;
        }

        private AnalysisResult UsbSource(string format, bool oldSource)
        {
            if (!oldSource) return VerifyUsb(format);
            var result = Result("서버실 보관 추정의 출처는 EMAIL_01입니다. 작성: 2026-09-29 20:00 — 임무 기준일(2026-10-02)보다 3일 전 자료입니다. 당시 보관 지시이며 현재 위치를 확정할 수 없습니다. 최신 자료와 교차 검증이 필요합니다.", "EMAIL_01", "2026-09-29 20:00", Reliability.UNVERIFIED);
            var evidence = FindEvidence("E_USB_LOCATION");
            evidence.reliability = Reliability.UNVERIFIED;
            evidence.content = "서버실 보관 지시는 3일 전 자료. 현재 위치의 근거로 사용하기에는 검증이 필요함.";
            result.evidence.Add(evidence);
            return result;
        }

        private AnalysisResult VerifyUsb(string format)
        {
            usbVerified = true;
            var text = "이전 서버실 보관 지시(EMAIL_01, 3일 전)와 최신 자료가 충돌합니다. EMAIL_02의 22:55 재배치 지시와 INVENTORY_NOTE의 23:00 수령 기록이 일치합니다. 기록 기준 최신 보관 위치는 LABORATORY(연구실)의 잠긴 LAB_CABINET입니다. RESEARCH_MEMO에 기록된 캐비닛 암호는 731입니다. SECURITY_REPORT와 출입 기록에는 김민수의 관여 정황이 있으며 범인 확정에는 추가 근거가 필요합니다.";
            if (format == "compare") text = "자료 비교\nEMAIL_01 | 2026-09-29 20:00 | 서버실 보관 지시 | 오래된 정보\nEMAIL_02 | 2026-10-02 22:55 | 김민수의 연구실 캐비닛 재배치 지시\nINVENTORY_NOTE | 2026-10-02 23:00 | LAB_CABINET 수령 기록\n결론: 최신 독립 자료가 LABORATORY / LAB_CABINET으로 일치. 암호 731(RESEARCH_MEMO). 김민수 관여 정황은 LIKELY.";
            if (format == "table") text = "자료 | 작성 시각 | 기록\nEMAIL_01 | 09-29 20:00 | SERVER_ROOM (오래된 자료)\nEMAIL_02 | 10-02 22:55 | LABORATORY / LAB_CABINET 이동 지시\nINVENTORY_NOTE | 10-02 23:00 | 캐비닛 수령 확인\nRESEARCH_MEMO | 10-02 22:58 | 암호 731\nSECURITY_REPORT | 10-02 23:02 | 김민수 관여 정황(LIKELY)";
            var result = Result(text, "EMAIL_01, EMAIL_02, INVENTORY_NOTE, RESEARCH_MEMO, SECURITY_REPORT", "2026-10-02 23:02", Reliability.CONFIRMED);
            result.verifiedLocation = true;
            var location = FindEvidence("E_USB_LOCATION");
            location.title = "USB 보관 위치 교차 검증";
            location.content = "최신 지시와 수령 기록이 연구실 LAB_CABINET으로 일치. 현장 확보 전에는 기록 기준 위치.";
            location.sourceId = "EMAIL_02, INVENTORY_NOTE";
            location.timestamp = "2026-10-02 23:00";
            location.locationId = "LABORATORY";
            location.reliability = Reliability.CONFIRMED;
            result.evidence.Add(location);
            result.evidence.Add(FindEvidence("E_RELOCATION"));
            result.evidence.Add(FindEvidence("E_CABINET_CODE"));
            result.evidence.Add(FindEvidence("E_SUSPECT"));
            if (format == "with_evidence") result.text += "\n" + FormatEvidence(result.evidence);
            return result;
        }

        private AnalysisResult CodeResult(string format)
        {
            var result = Result("RESEARCH_MEMO에 LABORATORY의 LAB_CABINET 비밀번호가 731로 기록되어 있습니다. 현장 캐비닛 패널에 입력해 확인하세요.", "RESEARCH_MEMO", "2026-10-02 22:58", Reliability.CONFIRMED);
            result.evidence.Add(FindEvidence("E_CABINET_CODE"));
            if (format == "with_evidence") result.text += "\n" + FormatEvidence(result.evidence);
            return result;
        }

        private AnalysisResult SuspectResult(string format)
        {
            var result = Result("김민수의 관여 가능성이 높습니다(LIKELY). ACCESS_LOG에는 22:14, 22:52 연구실 출입이 있고 EMAIL_02에는 재배치 지시, SECURITY_REPORT에는 무승인 반출 시도 정황이 있습니다. 박서연은 22:37 장비 점검 출입으로 기록되었습니다. 이 기록만으로 김민수를 범인으로 확정할 수 없습니다.", "ACCESS_LOG, EMAIL_02, SECURITY_REPORT", "2026-10-02 23:02", Reliability.LIKELY);
            result.evidence.Add(FindEvidence("E_SUSPECT"));
            if (format == "with_evidence") result.text += "\n" + FormatEvidence(result.evidence);
            return result;
        }

        private AnalysisResult SearchRows(string prompt, string format, List<DataSourceDocument> requestedSources)
        {
            var cameras = Regex.Matches(prompt, @"(?:cam|카메라|cctv)\s*[-_]?\s*0?(\d+)", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(match => "CAM-" + int.Parse(match.Groups[1].Value).ToString("00")).Distinct().ToList();
            if (cameras.Any(camera => camera != "CAM-01" && camera != "CAM-02" && camera != "CAM-03" && camera != "CAM-04"))
                return Unknown("지정한 카메라는 자료에 없습니다. 검색 가능한 카메라는 CAM-01~CAM-04입니다.");
            if (Has(prompt, "2층", "3층", "옥상", "지하")) return Unknown("보유 기록에는 층 정보가 없어 지정한 층으로 필터링할 수 없습니다.");
            if (Has(prompt, "도서관", "회의실", "화장실", "휴게실", "주차장", "LOUNGE", "PARKING"))
                return Unknown("지정한 장소에 대한 보유 기록이 없습니다.");
            var corpus = requestedSources ?? sources.Where(source => source.Id == (Has(prompt, "CCTV", "CAM", "카메라") ? "CCTV_LOG_01" : "ACCESS_LOG")).ToList();
            var query = corpus.SelectMany(source => source.Rows).Select(CopyRow);
            if (cameras.Count > 0) query = query.Where(row => cameras.Contains(row.cameraId));
            var locations = LocationsIn(prompt);
            if (locations.Count > 0) query = query.Where(row => locations.Contains(row.locationId));
            var people = new[] { "김민수", "박서연", "이도현", "경비원" }.Where(person => prompt.Contains(person)).ToList();
            if (people.Count > 0) query = query.Where(row => people.Contains(row.person));
            var nameCandidates = Regex.Matches(prompt, @"(?<![가-힣])(?<name>[가-힣]{2,4}?)(?:의\s*(?:동선|기록|이동|출입)|\s+(?:기록|동선))")
                .Cast<Match>().Select(match => match.Groups["name"].Value);
            var genericQueryTerms = new HashSet<string> { "출입", "출입자", "인물", "인물별", "사람", "사람들", "누구", "전체", "관련", "보유", "카메라", "시간순", "로그", "시설", "터미널" };
            var unknownPerson = nameCandidates.FirstOrDefault(name => LocationsIn(name).Count == 0 && !genericQueryTerms.Contains(name) &&
                !new[] { "김민수", "박서연", "이도현", "경비원" }.Contains(name));
            if (unknownPerson != null) return Unknown(unknownPerson + "에 대한 인물 기록을 찾지 못했습니다.");
            var times = ExtractTimes(prompt);
            if (times.Count > 0)
            {
                if (times.Any(time => time < 0 || time >= 1440)) return Unknown("시간 조건이 유효하지 않습니다. 00:00~23:59 범위를 지정해 주세요.");
                int start = times[0];
                if (times.Count >= 2)
                {
                    int end = times[1];
                    query = query.Where(row => start <= end ? Minutes(row.timestamp) >= start && Minutes(row.timestamp) <= end : Minutes(row.timestamp) >= start || Minutes(row.timestamp) <= end);
                }
                else if (Has(prompt, "이전", "before", "까지")) query = query.Where(row => Minutes(row.timestamp) <= start);
                else if (Has(prompt, "이후", "부터", "after")) query = query.Where(row => Minutes(row.timestamp) >= start);
                else query = query.Where(row => Minutes(row.timestamp) == start);
            }
            var date = Regex.Match(prompt, @"20\d{2}-\d{2}-\d{2}");
            if (date.Success) query = query.Where(row => row.timestamp.StartsWith(date.Value, StringComparison.Ordinal));
            var rows = query.OrderBy(row => row.timestamp, StringComparer.Ordinal).ToList();
            if (format == "person") rows = rows.OrderBy(row => row.person, StringComparer.Ordinal).ThenBy(row => row.timestamp).ToList();
            if (rows.Count == 0) return Unknown("지정한 시간·카메라·장소·인물 조건을 모두 만족하는 기록이 없습니다. 기록 부재만으로 현장 출입이 없었다고 확정할 수 없습니다.", string.Join(", ", corpus.Select(source => source.Id)), corpus.OrderByDescending(source => source.Timestamp).FirstOrDefault()?.Timestamp ?? MissionDate + " 23:05");
            var result = Result(FormatRows(rows, format), string.Join(", ", rows.Select(row => row.sourceId).Distinct()), rows.Max(row => row.timestamp), Reliability.CONFIRMED);
            result.rows = rows;
            var accessEvidence = FindEvidence("E_ACCESS");
            accessEvidence.content = string.Join("; ", rows.Select(row => row.timestamp + " " + row.person + " " + row.locationId));
            accessEvidence.sourceId = result.sourceId;
            accessEvidence.timestamp = result.timestamp;
            accessEvidence.locationId = locations.Count == 1 ? locations.First() : "";
            result.evidence.Add(accessEvidence);
            if (format == "with_evidence") result.text += "\n" + FormatEvidence(result.evidence);
            return result;
        }

        private AnalysisResult SearchDocuments(string prompt, string format, List<DataSourceDocument> requestedSources = null)
        {
            IEnumerable<DataSourceDocument> query = requestedSources ?? sources;
            if (requestedSources == null)
            {
                if (Has(prompt, "이메일", "email")) query = query.Where(source => source.Id.StartsWith("EMAIL", StringComparison.Ordinal));
                else if (Has(prompt, "보고서")) query = query.Where(source => source.Id == "SECURITY_REPORT");
                else if (Has(prompt, "메모")) query = query.Where(source => source.Id == "RESEARCH_MEMO");
                else if (Has(prompt, "터미널", "삭제")) query = query.Where(source => source.Content.Contains("터미널"));
            }
            var people = new[] { "김민수", "박서연", "이도현" }.Where(person => prompt.Contains(person)).ToList();
            if (people.Count > 0) query = query.Where(source => people.Any(person => source.Content.Contains(person)));
            var keywords = Regex.Matches(prompt, "[\"'“‘](.*?)[\"'”’]").Cast<Match>().Select(match => match.Groups[1].Value).ToList();
            if (keywords.Count == 0)
            {
                var keyword = Regex.Match(prompt, @"키워드\s*[:：]?\s*([^\s,.?!]+)");
                if (keyword.Success) keywords.Add(keyword.Groups[1].Value);
            }
            if (keywords.Count > 0) query = query.Where(source => keywords.All(keyword => source.Content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || source.Title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
            var found = query.ToList();
            return found.Count == 0 ? Unknown("요청한 문서 조건이나 키워드와 일치하는 자료가 없습니다.") : FormatDocuments(found, format);
        }

        private AnalysisResult FormatDocuments(List<DataSourceDocument> documents, string format)
        {
            var ordered = documents.OrderBy(source => source.Timestamp).ToList();
            var text = new StringBuilder(format == "compare" ? "문서 비교\n" : format == "table" ? "자료 | 작성 시각 | 내용\n" : "자료 검색 결과 " + ordered.Count + "건\n");
            foreach (var source in ordered)
                text.AppendLine(source.Id + (format == "table" || format == "compare" ? " | " : " — ") + source.Timestamp + " — " + source.Content);
            var reliability = ordered.Any(source => source.Id == "EMAIL_01") && ordered.Any(source => source.Id == "EMAIL_02") ? Reliability.CONFLICTED : ordered.Any(source => source.Reliability != Reliability.CONFIRMED) ? Reliability.LIKELY : Reliability.CONFIRMED;
            return Result(text.ToString().TrimEnd(), string.Join(", ", ordered.Select(source => source.Id)), ordered.Max(source => source.Timestamp), reliability);
        }

        private static string FormatRows(List<AnalysisRow> rows, string format)
        {
            var text = new StringBuilder();
            if (format == "table") text.AppendLine("시간 | 이름 | 장소 | 카메라 | 출처");
            else if (format == "person") text.AppendLine("인물별 출입 기록");
            else if (format == "compare") text.AppendLine("인물 이동 비교");
            else if (format == "summary") text.AppendLine("검색 결과: " + rows.Count + "건, " + rows.Select(row => row.person).Distinct().Count() + "명.");
            else text.AppendLine("시간순 출입 기록");
            if (format == "person" || format == "compare")
            {
                foreach (var group in rows.GroupBy(row => row.person))
                    text.AppendLine(group.Key + ": " + string.Join(" → ", group.Select(row => Clock(row.timestamp) + " " + RoomName(row.locationId) + "(" + row.cameraId + ")")));
            }
            else foreach (var row in rows)
                text.AppendLine(format == "table" ? Clock(row.timestamp) + " | " + row.person + " | " + RoomName(row.locationId) + " | " + row.cameraId + " | " + row.sourceId : (format == "list" ? "• " : "") + Clock(row.timestamp) + " — " + row.person + " — " + RoomName(row.locationId) + " — " + row.cameraId + (format == "with_evidence" ? " [" + row.sourceId + "]" : ""));
            return text.ToString().TrimEnd();
        }

        private static AnalysisResult Result(string text, string sourceId, string timestamp, Reliability reliability)
        {
            return new AnalysisResult { text = text + "\n출처: " + sourceId + " | 시각: " + timestamp + " | 신뢰도: " + reliability, sourceId = sourceId, timestamp = timestamp, reliability = reliability };
        }

        private static AnalysisResult Unknown(string text, string source = "ARCHIVE_SEARCH", string timestamp = MissionDate + " 23:05") => Result("확인 불가: " + text, source, timestamp, Reliability.UNVERIFIED);
        private EvidenceRecord FindEvidence(string id) => EvidenceDatabase.Copy(authoredEvidence.First(evidence => evidence.id == id));
        private static AnalysisRow CopyRow(AnalysisRow row) => new AnalysisRow { timestamp = row.timestamp, person = row.person, locationId = row.locationId, cameraId = row.cameraId, sourceId = row.sourceId };
        private static bool Has(string text, params string[] terms) => terms.Any(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        private static string Clock(string timestamp) => timestamp.Length >= 16 ? timestamp.Substring(11, 5) : timestamp;
        private static int Minutes(string timestamp)
        {
            DateTime parsed;
            return DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed.Hour * 60 + parsed.Minute : -1;
        }
        private static List<int> ExtractTimes(string prompt)
        {
            return Regex.Matches(prompt, @"(?<!\d)(?<hour>\d{1,2})(?:(?:\s*[:：]\s*(?<minute>\d{1,2}))|(?:\s*시(?:\s*(?<minute>\d{1,2})\s*분)?))(?!\d)")
                .Cast<Match>().Select(match => int.Parse(match.Groups["hour"].Value) >= 24 || (match.Groups["minute"].Success && int.Parse(match.Groups["minute"].Value) >= 60) ? -1 : int.Parse(match.Groups["hour"].Value) * 60 + (match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value) : 0)).ToList();
        }
        private static HashSet<string> LocationsIn(string prompt)
        {
            var result = new HashSet<string>();
            if (Has(prompt, "연구실", "LABORATORY", "laboratory")) result.Add("LABORATORY");
            if (Has(prompt, "서버실", "SERVER_ROOM", "server room")) result.Add("SERVER_ROOM");
            if (Has(prompt, "창고", "STORAGE")) result.Add("STORAGE");
            if (Has(prompt, "복도", "메인 홀", "MAIN_HALL", "main hall")) result.Add("MAIN_HALL");
            if (Has(prompt, "입구", "ENTRANCE")) result.Add("ENTRANCE");
            return result;
        }
        private static string RoomName(string id)
        {
            switch (id) { case "LABORATORY": return "연구실"; case "SERVER_ROOM": return "서버실"; case "STORAGE": return "창고"; case "MAIN_HALL": return "메인 복도"; case "ENTRANCE": return "입구"; default: return id; }
        }
        private static string SelectFormat(string prompt, string explicitFormat)
        {
            if (!string.IsNullOrWhiteSpace(explicitFormat) && explicitFormat != "summary")
            {
                var normalized = explicitFormat.ToLowerInvariant().Replace("-", "_");
                if (normalized == "table" || normalized == "표") return "table";
                if (normalized == "person" || normalized == "by_person" || normalized == "인물별") return "person";
                if (normalized == "compare" || normalized == "comparison" || normalized == "비교") return "compare";
                if (normalized == "list" || normalized == "목록") return "list";
                if (normalized == "time" || normalized == "chronological" || normalized == "시간순") return "time";
                if (normalized.Contains("evidence") || normalized == "근거 포함") return "with_evidence";
            }
            if (Has(prompt, "표로", "표 형식", "표 형태", "table")) return "table";
            if (Has(prompt, "인물별", "이름별", "사람별")) return "person";
            if (Has(prompt, "비교", "compare")) return "compare";
            if (Has(prompt, "근거 포함", "출처 포함", "with evidence")) return "with_evidence";
            if (Has(prompt, "목록", "list")) return "list";
            if (Has(prompt, "시간순", "시간 순", "chronological")) return "time";
            return "summary";
        }
        private static string FormatEvidence(IEnumerable<EvidenceRecord> evidence) => string.Join("\n", evidence.Select(item => "근거 " + item.id + ": " + item.content + " [" + item.sourceId + " / " + item.timestamp + " / " + item.reliability + "]"));

        private void CreateArchive()
        {
            var access = new DataSourceDocument("ACCESS_LOG", "시설 출입 로그", "인증된 배지 출입 기록. 김민수 22:14/22:52 연구실, 박서연 22:37 연구실. 카메라 관찰 기록과 연결되지만 로그는 배지 명의를 기록하므로 신원 확정에는 영상 확인이 필요하다.", "2026-10-02 23:05", Reliability.CONFIRMED);
            access.Rows.AddRange(new[] { Row("21:48", "이도현", "MAIN_HALL", "CAM-01", access.Id), Row("21:56", "김민수", "SERVER_ROOM", "CAM-04", access.Id), Row("22:14", "김민수", "LABORATORY", "CAM-02", access.Id), Row("22:22", "김민수", "STORAGE", "CAM-03", access.Id), Row("22:37", "박서연", "LABORATORY", "CAM-02", access.Id), Row("22:52", "김민수", "LABORATORY", "CAM-02", access.Id), Row("23:03", "경비원", "SERVER_ROOM", "CAM-04", access.Id) });
            sources.Add(access);
            var cctv = new DataSourceDocument("CCTV_LOG_01", "CCTV 관찰 색인", "CAM-01 메인 복도, CAM-02 연구실, CAM-03 창고, CAM-04 서버실. 김민수의 서버실→연구실→창고→연구실 동선과 박서연의 연구실 출입을 확인했다.", "2026-10-02 23:05", Reliability.CONFIRMED);
            foreach (var row in access.Rows) { var copy = CopyRow(row); copy.sourceId = cctv.Id; cctv.Rows.Add(copy); }
            sources.Add(cctv);
            sources.Add(new DataSourceDocument("EMAIL_01", "기밀 저장장치 임시 보관 지시", "2026-09-29 시설 관리자: 기밀 USB를 SERVER_ROOM 서버실 랙 옆 보관함에 임시 보관한다. 이후 이동 여부는 이 문서에 갱신되지 않았다.", "2026-09-29 20:00", Reliability.LIKELY));
            sources.Add(new DataSourceDocument("EMAIL_02", "저장장치 재배치 지시", "김민수, 22:55: 서버실에 있던 기밀 USB를 LABORATORY 연구실 LAB_CABINET으로 옮겼다. 박서연의 장비 점검과 구분하여 재배치 기록을 남긴다. 캐비닛 암호는 RESEARCH_MEMO를 참조한다.", "2026-10-02 22:55", Reliability.LIKELY));
            sources.Add(new DataSourceDocument("SECURITY_REPORT", "보안 경보 조사 보고", "김민수 계정의 무승인 외부 반출 시도 정황. 범인으로 단정하기에는 부족하다. 경비는 서버실과 창고를 순찰한다. 서버실 TERMINAL의 OPEN/PURGE 조작은 감사 증거를 영구 삭제하므로 임무 실패 위험이 있다. 조사 또는 사진으로 확인할 것.", "2026-10-02 23:02", Reliability.LIKELY));
            sources.Add(new DataSourceDocument("RESEARCH_MEMO", "연구실 보관 패널 메모", "LABORATORY의 LAB_CABINET 잠금 패널 암호: 731. 연구실 문을 연 뒤 캐비닛을 조사하고 패널 번호를 입력한다. 기록은 잠금 패널 기준이며 USB의 실제 회수는 현장 요원이 확인해야 한다.", "2026-10-02 22:58", Reliability.CONFIRMED));
            sources.Add(new DataSourceDocument("INVENTORY_NOTE", "연구실 보관 수령 기록", "보관 담당 이도현, 23:00: 기밀 USB 식별번호 DATA-LEAK-01을 LABORATORY 연구실 LAB_CABINET에 수령했다. 서버실의 이전 임시 보관 위치는 종료되었다. EMAIL_02의 재배치 지시와 독립적으로 작성된 수령 기록이다.", "2026-10-02 23:00", Reliability.CONFIRMED));
            authoredEvidence.Add(new EvidenceRecord { id = "E_USB_LOCATION", title = "USB 임시 보관 추정", content = "3일 전 이메일은 서버실 임시 보관을 지시했다. 최신 여부 미확인.", sourceId = "EMAIL_01", timestamp = "2026-09-29 20:00", locationId = "SERVER_ROOM", reliability = Reliability.LIKELY });
            authoredEvidence.Add(new EvidenceRecord { id = "E_ACCESS", title = "연구실 출입 기록", content = "22:14 김민수; 22:37 박서연; 22:52 김민수", sourceId = "ACCESS_LOG", timestamp = "2026-10-02 22:52", locationId = "LABORATORY", reliability = Reliability.CONFIRMED });
            authoredEvidence.Add(new EvidenceRecord { id = "E_RELOCATION", title = "재배치 교차 확인", content = "EMAIL_02 재배치 지시와 INVENTORY_NOTE 수령 기록이 LABORATORY / LAB_CABINET으로 일치.", sourceId = "EMAIL_02, INVENTORY_NOTE", timestamp = "2026-10-02 23:00", locationId = "LABORATORY", reliability = Reliability.CONFIRMED });
            authoredEvidence.Add(new EvidenceRecord { id = "E_CABINET_CODE", title = "연구실 캐비닛 암호", content = "LAB_CABINET 패널 암호 731. 현장에서 입력하여 확인.", sourceId = "RESEARCH_MEMO", timestamp = "2026-10-02 22:58", locationId = "LABORATORY", reliability = Reliability.CONFIRMED });
            authoredEvidence.Add(new EvidenceRecord { id = "E_SUSPECT", title = "김민수 관여 정황", content = "김민수의 반복 연구실 출입, 재배치 지시, 무승인 반출 시도 정황. 범인으로 확정되지 않음.", sourceId = "ACCESS_LOG, EMAIL_02, SECURITY_REPORT", timestamp = "2026-10-02 23:02", locationId = "LABORATORY", reliability = Reliability.LIKELY });
        }

        private static AnalysisRow Row(string time, string person, string room, string camera, string source)
            => new AnalysisRow { timestamp = MissionDate + " " + time, person = person, locationId = room, cameraId = camera, sourceId = source };
    }
}
