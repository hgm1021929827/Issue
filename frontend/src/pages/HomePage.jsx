import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { BookmarkPlus, CalendarDays, ClipboardList, FolderKanban, ListTodo, Sparkles } from "lucide-react";
import { api } from "../api.js";
import MonthCalendar from "../components/MonthCalendar.jsx";
import PageTitle from "../components/PageTitle.jsx";

function toIsoDate(year, month, day) {
  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function itemDate(item) {
  return item.date || item.dueDate;
}

function formatMonthDay(date) {
  const text = String(date || "");
  const match = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return match ? `${match[2]}-${match[3]}` : text;
}

function dueClass(dueDate) {
  if (!dueDate) return "due muted";
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const due = new Date(dueDate);
  due.setHours(0, 0, 0, 0);
  const diff = (due - today) / 86400000;
  if (diff < 0) return "due is-overdue";
  if (diff <= 3) return "due is-soon";
  return "due";
}

function workTypeLabel(type) {
  if (type === "project") return "專案";
  if (type === "projectIssue") return "專案議題";
  if (type === "workItem") return "工作項次";
  return "議題";
}

function homeTargetLabel(item) {
  if (item.targetType === "member") {
    return `${item.targetName}（${item.targetSecondaryName}）`;
  }
  return item.targetSecondaryName ? `${item.targetName}（${item.targetSecondaryName}）` : item.targetName;
}

function DueMark({ date }) {
  if (!date) return null;
  return <em className={dueClass(date)}>{formatMonthDay(date)}</em>;
}

function HomeChildGroup({ title, items, renderItem }) {
  const [open, setOpen] = useState(items.length <= 3);
  const shown = open ? items : items.slice(0, 3);
  return (
    <div className="home-child-group">
      <div className="home-child-label">
        <span>{title}</span>
        <span>{items.length}</span>
      </div>
      <ul className="home-child-list">
        {shown.map(renderItem)}
      </ul>
      {items.length > 3 && (
        <button className="home-child-more" type="button" onClick={() => setOpen(!open)}>
          {open ? "收合" : `還有 ${items.length - 3} 筆`}
        </button>
      )}
    </div>
  );
}

function trackHref(item) {
  if (item.workType === "issue") return `/issues/${item.workId}?trackTodo=${item.id}`;
  if (item.workType === "project") return `/projects/${item.workId}?trackTodo=${item.id}`;
  if (item.workType === "workItem") return `/projects/${item.projectId}?workItem=${item.workId}&trackTodo=${item.id}`;
  return `/projects/${item.projectId}?item=${item.workId}&trackTodo=${item.id}`;
}

export default function HomePage() {
  const now = new Date();
  const [tab, setTab] = useState("projects");
  const [majors, setMajors] = useState([]);
  const [filter, setFilter] = useState("");
  const [projects, setProjects] = useState([]);
  const [issues, setIssues] = useState([]);
  const [tracks, setTracks] = useState([]);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [marks, setMarks] = useState([]);
  const [views, setViews] = useState({
    project: true,
    projectIssue: true,
    workItem: true,
    track: true,
    issue: true
  });
  const [pickingIssue, setPickingIssue] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const loadCalendar = async (y = year, m = month) => {
    setMarks(await api.calendar(y, m));
  };

  const loadList = async () => {
    setError("");
    setLoading(true);
    try {
      const [majorList, projectList, issueList, trackList] = await Promise.all([
        api.majorCategories(),
        api.projects(filter || undefined),
        api.issues(filter || undefined),
        api.homeTrackTodos()
      ]);
      const nested = await Promise.all(
        projectList.map((item) => Promise.all([api.projectItems(item.id), api.projectWorkItems(item.id)]))
      );
      setMajors(majorList);
      setProjects(projectList.map((item, index) => ({
        ...item,
        items: nested[index][0] || [],
        workItems: nested[index][1] || []
      })));
      setIssues(issueList);
      setTracks(trackList);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadList();
  }, [filter]);

  useEffect(() => {
    loadCalendar().catch((err) => setError(err.message));
  }, [year, month]);

  const addPlanDates = async (issueId, dates) => {
    if (!dates?.length) return false;
    try {
      await api.addPlans(issueId, dates);
      if (!views.issue) setViews((prev) => ({ ...prev, issue: true }));
      await loadCalendar();
      setError("");
      return true;
    } catch (err) {
      setError(err.message);
      return false;
    }
  };

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      <div className="page-grid">
        <section>
          <PageTitle icon={ListTodo}>工作清單</PageTitle>
          <div className="filters member-tabs" role="tablist" aria-label="首頁清單">
            <button
              type="button"
              role="tab"
              className={tab === "projects" ? "chip active" : "chip"}
              aria-selected={tab === "projects"}
              onClick={() => setTab("projects")}
            >
              <FolderKanban size={14} strokeWidth={1.75} aria-hidden="true" />
              專案（{projects.length}）
            </button>
            <button
              type="button"
              role="tab"
              className={tab === "issues" ? "chip active" : "chip"}
              aria-selected={tab === "issues"}
              onClick={() => setTab("issues")}
            >
              <ClipboardList size={14} strokeWidth={1.75} aria-hidden="true" />
              正式議題（{issues.length}）
            </button>
            <button
              type="button"
              role="tab"
              className={tab === "tracks" ? "chip active" : "chip"}
              aria-selected={tab === "tracks"}
              onClick={() => setTab("tracks")}
            >
              <BookmarkPlus size={14} strokeWidth={1.75} aria-hidden="true" />
              追蹤項目（{tracks.length}）
            </button>
          </div>
          {tab !== "tracks" && (
            <div className="filters">
              <button className={!filter ? "chip active" : "chip"} type="button" onClick={() => setFilter("")}>
                全部
              </button>
              {majors.map((m) => (
                <button
                  key={m.id}
                  type="button"
                  className={String(filter) === String(m.id) ? "chip active" : "chip"}
                  onClick={() => setFilter(m.id)}
                >
                  {m.name}
                </button>
              ))}
            </div>
          )}
          {tab === "issues" && (
            <p className="list-hint muted">拖曳日曆按鈕到格子可排單日；點按鈕後再點月曆可排多天。</p>
          )}
          {loading && <p className="muted">載入中…</p>}
          {!loading && tab === "projects" && projects.length === 0 && (
            <div className="empty">
              <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
              <p>尚無專案。</p>
              <Link className="btn" to="/projects/new">
                立即新增
              </Link>
            </div>
          )}
          {!loading && tab === "projects" && projects.length > 0 && (
            <ul className="home-project-list">
              {projects.map((item) => (
                <li key={item.id} className="home-project-card">
                  <Link
                    className="home-project-head"
                    to={`/projects/${item.id}`}
                    style={{ "--accent-color": item.subCategoryColor || item.majorCategoryColor }}
                  >
                    <span className="home-project-meta">
                      <span className="tag">{item.subCategoryName || item.majorCategoryName}</span>
                      <span className="home-project-code">{item.code}</span>
                    </span>
                    <strong className="home-project-name">{item.name}</strong>
                    {item.clientCompanyName ? (
                      <span className="home-project-vendor">{item.clientCompanyName}</span>
                    ) : null}
                  </Link>
                  {item.items?.length > 0 && (
                    <HomeChildGroup
                      title="專案議題"
                      items={item.items}
                      renderItem={(child) => (
                        <li key={child.id}>
                          <Link to={`/projects/${item.id}?item=${child.id}`}>
                            <span className="issue-id">{child.seqNo}</span>
                            <span className="home-child-title">{child.title}</span>
                            <DueMark date={child.dueDate} />
                          </Link>
                        </li>
                      )}
                    />
                  )}
                  {item.workItems?.length > 0 && (
                    <HomeChildGroup
                      title="工作項次"
                      items={item.workItems}
                      renderItem={(child) => (
                        <li key={child.id}>
                          <Link to={`/projects/${item.id}?workItem=${child.id}`}>
                            <span className="issue-id">{child.workItemCode}</span>
                            <span className="home-child-title">{child.title || "（無說明）"}</span>
                            <DueMark date={child.dueDate} />
                          </Link>
                        </li>
                      )}
                    />
                  )}
                </li>
              ))}
            </ul>
          )}
          {!loading && tab === "issues" && issues.length === 0 && (
            <div className="empty">
              <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
              <p>尚無議題。</p>
              <Link className="btn" to="/issues/new">
                立即新增
              </Link>
            </div>
          )}
          {!loading && tab === "issues" && issues.length > 0 && (
            <ul className="issue-list">
              {issues.map((item) => (
                <li
                  key={item.id}
                  className={`issue-row${pickingIssue?.id === item.id ? " is-picking" : ""}`}
                >
                  <Link
                    to={`/issues/${item.id}`}
                    style={{ "--accent-color": item.subCategoryColor || item.majorCategoryColor }}
                  >
                    <span className="tag">{item.subCategoryName || item.majorCategoryName}</span>
                    <strong>
                      <span className="issue-id">#{item.issueNo || item.id}</span>
                      {item.title}
                      {item.clientCompanyName ? <span className="list-vendor">{item.clientCompanyName}</span> : null}
                    </strong>
                    <em className={dueClass(item.dueDate)}>{item.dueDate ? formatMonthDay(item.dueDate) : "無預計日"}</em>
                  </Link>
                  <button
                    className="plan-btn"
                    type="button"
                    draggable
                    aria-label={`把 ${item.title} 排入預計項目`}
                    onClick={() => setPickingIssue(item)}
                    onDragStart={(event) => {
                      event.dataTransfer.setData("application/json", JSON.stringify({ id: item.id, title: item.title }));
                    }}
                  >
                    <CalendarDays size={16} strokeWidth={1.75} aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
          {!loading && tab === "tracks" && tracks.length === 0 && (
            <div className="empty">
              <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
              <p>目前沒有需要追蹤的項目。</p>
            </div>
          )}
          {!loading && tab === "tracks" && tracks.length > 0 && (
            <ul className="issue-list home-track-list">
              {tracks.map((item) => (
                <li key={item.id} className="issue-row">
                  <Link to={trackHref(item)}>
                    <span className="tag home-type-tag">{workTypeLabel(item.workType)}</span>
                    <strong>
                      {item.title}
                      {item.workLabel ? <span className="list-vendor">{item.workLabel}</span> : null}
                      {homeTargetLabel(item) ? <span className="list-vendor">{homeTargetLabel(item)}</span> : null}
                    </strong>
                    {item.reminderDate ? (
                      <em className={dueClass(item.reminderDate)}>{formatMonthDay(item.reminderDate)}</em>
                    ) : null}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
        <MonthCalendar
          year={year}
          month={month}
          items={marks}
          views={views}
          pickingIssue={pickingIssue}
          onToggleView={(key) => setViews((prev) => ({ ...prev, [key]: !prev[key] }))}
          onPrev={() => {
            const d = new Date(year, month - 2, 1);
            setYear(d.getFullYear());
            setMonth(d.getMonth() + 1);
          }}
          onNext={() => {
            const d = new Date(year, month, 1);
            setYear(d.getFullYear());
            setMonth(d.getMonth() + 1);
          }}
          onConfirmPick={async (dates) => {
            if (!pickingIssue) return;
            const ok = await addPlanDates(pickingIssue.id, dates);
            if (ok) setPickingIssue(null);
          }}
          onCancelPick={() => setPickingIssue(null)}
          onDropIssue={(day, issueId) => addPlanDates(issueId, [toIsoDate(year, month, day)])}
          onRemovePlan={async (item) => {
            try {
              await api.removePlan(item.id, itemDate(item));
              await loadCalendar();
              setError("");
            } catch (err) {
              setError(err.message);
            }
          }}
        />
      </div>
    </div>
  );
}
