import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { BookmarkPlus, CalendarCheck, CalendarDays, Check, ChevronLeft, ChevronRight, ClipboardList, FolderKanban, ListTodo, X } from "lucide-react";
import PageTitle from "./PageTitle.jsx";
import AppDateField from "./AppDateField.jsx";

function daysInMonth(year, month) {
  return new Date(year, month, 0).getDate();
}

function itemDate(item) {
  return item.date || item.dueDate;
}

function markPath(item) {
  if (item.source === "trackTodo") {
    if (item.workType === "project") return `/projects/${item.workId}?trackTodo=${item.id}`;
    if (item.workType === "projectIssue") return `/projects/${item.projectId}?item=${item.workId}&trackTodo=${item.id}`;
    if (item.workType === "workItem") return `/projects/${item.projectId}?workItem=${item.workId}&trackTodo=${item.id}`;
    return `/issues/${item.workId}?trackTodo=${item.id}`;
  }
  if (item.source === "project") return `/projects/${item.id}`;
  if (item.source === "projectIssue") return `/projects/${item.projectId}?item=${item.id}`;
  if (item.source === "workItem") return `/projects/${item.projectId}?workItem=${item.id}`;
  return `/issues/${item.id}`;
}

function clipText(text, max) {
  const value = String(text || "").trim();
  if (value.length <= max) return value;
  return `${value.slice(0, max)}…`;
}

function lastToken(label) {
  const parts = String(label || "").trim().split(/\s+/).filter(Boolean);
  return parts[parts.length - 1] || "";
}

function ownerCode(label) {
  const text = String(label || "").trim();
  const index = text.lastIndexOf(" ");
  return index < 0 ? "" : text.slice(0, index).trim();
}

function personName(text) {
  const value = String(text || "").trim();
  return value.split(/[（(]/)[0].trim();
}

function markLabel(item) {
  if (item.source === "project") {
    return clipText(item.title || item.label, 8) || item.label;
  }
  if (item.source === "projectIssue" || item.source === "workItem") {
    return [lastToken(item.label), clipText(item.title, 6)].filter(Boolean).join(" ");
  }
  if (item.source === "trackTodo") {
    return [personName(item.content), clipText(item.title, 6)].filter(Boolean).join(" ");
  }
  return [`#${item.issueNo || item.id}`, clipText(item.title, 6)].filter(Boolean).join(" ");
}

function markHover(item) {
  if (item.source === "project") {
    return [item.label, item.title].filter(Boolean).join(" ");
  }
  if (item.source === "projectIssue" || item.source === "workItem") {
    return [ownerCode(item.label), lastToken(item.label), item.title].filter(Boolean).join(" · ");
  }
  if (item.source === "trackTodo") {
    return [item.title, item.label, item.content].filter(Boolean).join(" · ");
  }
  return [`#${item.issueNo || item.id}`, item.title].filter(Boolean).join(" ");
}

function markAria(item) {
  if (item.source === "trackTodo") return `追蹤 ${markHover(item)}`;
  if (item.source === "project") return `專案 ${markHover(item)}`;
  if (item.source === "projectIssue") return `專案議題 ${markHover(item)}`;
  if (item.source === "workItem") return `工作項次 ${markHover(item)}`;
  return `議題 ${markHover(item)}`;
}

function markVisible(item, views) {
  const source = item.source || "issue";
  if (source === "project") return views.project;
  if (source === "projectIssue") return views.projectIssue;
  if (source === "workItem") return views.workItem;
  if (source === "trackTodo") return views.track;
  return views.issue;
}

function pad(n) {
  return String(n).padStart(2, "0");
}

function toIso(year, month, day) {
  return `${year}-${pad(month)}-${pad(day)}`;
}

function enumerateDates(fromIso, toIso) {
  const start = new Date(`${fromIso}T00:00:00`);
  const end = new Date(`${toIso}T00:00:00`);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return [];
  const a = start <= end ? start : end;
  const b = start <= end ? end : start;
  const out = [];
  const cur = new Date(a);
  while (cur <= b) {
    out.push(`${cur.getFullYear()}-${pad(cur.getMonth() + 1)}-${pad(cur.getDate())}`);
    cur.setDate(cur.getDate() + 1);
    if (out.length > 62) break;
  }
  return out;
}

export default function MonthCalendar({
  year,
  month,
  items,
  views,
  pickingIssue,
  onToggleView,
  onPrev,
  onNext,
  onConfirmPick,
  onCancelPick,
  onDropIssue,
  onRemovePlan
}) {
  const [selected, setSelected] = useState(() => new Set());
  const [rangeFrom, setRangeFrom] = useState("");
  const [rangeTo, setRangeTo] = useState("");
  const [lastIso, setLastIso] = useState(null);

  useEffect(() => {
    setSelected(new Set());
    setRangeFrom("");
    setRangeTo("");
    setLastIso(null);
  }, [pickingIssue?.id]);

  const firstWeekday = new Date(year, month - 1, 1).getDay();
  const total = daysInMonth(year, month);
  const cells = [];
  for (let i = 0; i < firstWeekday; i += 1) cells.push(null);
  for (let d = 1; d <= total; d += 1) cells.push(d);
  while (cells.length % 7 !== 0) cells.push(null);

  const visible = items.filter((item) => markVisible(item, views));
  const byDay = {};
  for (const item of visible) {
    const day = Number(String(itemDate(item)).slice(-2));
    if (!byDay[day]) byDay[day] = [];
    byDay[day].push(item);
  }

  const today = new Date();
  const isToday = (day) =>
    day &&
    year === today.getFullYear() &&
    month === today.getMonth() + 1 &&
    day === today.getDate();

  const pendingDates = useMemo(() => {
    const extra = rangeFrom && rangeTo ? enumerateDates(rangeFrom, rangeTo) : [];
    return [...new Set([...selected, ...extra])].sort();
  }, [selected, rangeFrom, rangeTo]);

  const toggleDay = (event, day) => {
    if (!day || !pickingIssue) return;
    const iso = toIso(year, month, day);
    if (event.shiftKey && lastIso) {
      const range = enumerateDates(lastIso, iso);
      setSelected((prev) => {
        const next = new Set(prev);
        range.forEach((d) => next.add(d));
        return next;
      });
    } else {
      setSelected((prev) => {
        const next = new Set(prev);
        if (next.has(iso)) next.delete(iso);
        else next.add(iso);
        return next;
      });
    }
    setLastIso(iso);
  };

  const allowDrop = (event, day) => {
    if (!day) return;
    event.preventDefault();
    event.currentTarget.classList.add("is-drop");
  };

  const clearDrop = (event) => {
    event.currentTarget.classList.remove("is-drop");
  };

  const drop = (event, day) => {
    clearDrop(event);
    if (!day) return;
    event.preventDefault();
    const raw = event.dataTransfer.getData("application/json") || event.dataTransfer.getData("text/plain");
    if (!raw) return;
    try {
      const payload = JSON.parse(raw);
      if (payload?.id) onDropIssue(day, payload.id);
    } catch {
      const id = Number(raw);
      if (id) onDropIssue(day, id);
    }
  };

  const tooMany = pendingDates.length > 62;

  return (
    <section className={`calendar${pickingIssue ? " is-picking" : ""}`}>
      <div className="cal-views" role="group" aria-label="行事曆視角">
        <span className="cal-views-label">視角</span>
        <button
          type="button"
          className={`cal-view is-project${views.project ? " is-on" : ""}`}
          aria-pressed={views.project}
          onClick={() => onToggleView("project")}
        >
          <FolderKanban size={18} strokeWidth={1.9} aria-hidden="true" />
          專案
          {views.project && <Check size={16} strokeWidth={2.4} aria-hidden="true" />}
        </button>
        <button
          type="button"
          className={`cal-view is-project-issue${views.projectIssue ? " is-on" : ""}`}
          aria-pressed={views.projectIssue}
          onClick={() => onToggleView("projectIssue")}
        >
          <ClipboardList size={18} strokeWidth={1.9} aria-hidden="true" />
          專案議題
          {views.projectIssue && <Check size={16} strokeWidth={2.4} aria-hidden="true" />}
        </button>
        <button
          type="button"
          className={`cal-view is-work-item${views.workItem ? " is-on" : ""}`}
          aria-pressed={views.workItem}
          onClick={() => onToggleView("workItem")}
        >
          <ListTodo size={18} strokeWidth={1.9} aria-hidden="true" />
          工作項次
          {views.workItem && <Check size={16} strokeWidth={2.4} aria-hidden="true" />}
        </button>
        <button
          type="button"
          className={`cal-view is-track${views.track ? " is-on" : ""}`}
          aria-pressed={views.track}
          onClick={() => onToggleView("track")}
        >
          <BookmarkPlus size={18} strokeWidth={1.9} aria-hidden="true" />
          追蹤項目
          {views.track && <Check size={16} strokeWidth={2.4} aria-hidden="true" />}
        </button>
        <button
          type="button"
          className={`cal-view is-issue${views.issue ? " is-on" : ""}`}
          aria-pressed={views.issue}
          onClick={() => onToggleView("issue")}
        >
          <CalendarCheck size={18} strokeWidth={1.9} aria-hidden="true" />
          議題
          {views.issue && <Check size={16} strokeWidth={2.4} aria-hidden="true" />}
        </button>
      </div>
      <div className="row-between">
        <PageTitle as="h2" icon={CalendarDays}>
          {year} 年 {month} 月
        </PageTitle>
        <div className="btn-row">
          <button type="button" onClick={onPrev} aria-label="上月">
            <ChevronLeft size={16} strokeWidth={1.75} aria-hidden="true" />
            上月
          </button>
          <button type="button" onClick={onNext} aria-label="下月">
            下月
            <ChevronRight size={16} strokeWidth={1.75} aria-hidden="true" />
          </button>
        </div>
      </div>
      {pickingIssue && (
        <div className="cal-pick-hint">
          <div className="cal-pick-copy">
            把「{pickingIssue.title}」排入預計項目。點格子可多選（Shift 連選），或填區間一次加入。
            <span className="cal-pick-range">
              <label>
                從
                <AppDateField compact value={rangeFrom} onChange={setRangeFrom} placeholder="開始日" />
              </label>
              <label>
                到
                <AppDateField compact value={rangeTo} onChange={setRangeTo} placeholder="結束日" />
              </label>
            </span>
          </div>
          <div className="btn-row">
            <span className="cal-pick-count">{tooMany ? "一次最多 62 天" : `已選 ${pendingDates.length} 天`}</span>
            <button
              type="button"
              disabled={pendingDates.length === 0 || tooMany}
              onClick={() => onConfirmPick(pendingDates)}
            >
              加入 {Math.min(pendingDates.length, 62)} 天
            </button>
            <button type="button" onClick={onCancelPick}>
              取消
            </button>
          </div>
        </div>
      )}
      {!views.project && !views.projectIssue && !views.workItem && !views.track && !views.issue && (
        <p className="muted">請至少選擇一種視角。</p>
      )}
      <div className="cal-grid head">
        {["日", "一", "二", "三", "四", "五", "六"].map((d) => (
          <div key={d}>{d}</div>
        ))}
      </div>
      <div className="cal-grid">
        {cells.map((day, index) => {
          const iso = day ? toIso(year, month, day) : "";
          const picked = Boolean(day && pickingIssue && pendingDates.includes(iso));
          return (
            <div
              key={index}
              className={`cal-cell${day ? "" : " is-empty"}${isToday(day) ? " is-today" : ""}${picked ? " is-selected" : ""}`}
              role={pickingIssue && day ? "button" : undefined}
              tabIndex={pickingIssue && day ? 0 : undefined}
              aria-label={pickingIssue && day ? `選取 ${month} 月 ${day} 日` : undefined}
              aria-pressed={pickingIssue && day ? picked : undefined}
              onDragOver={(event) => allowDrop(event, day)}
              onDragLeave={clearDrop}
              onDrop={(event) => drop(event, day)}
              onClick={(event) => toggleDay(event, day)}
              onKeyDown={(event) => {
                if (!pickingIssue || !day) return;
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  toggleDay(event, day);
                }
              }}
            >
              {day && <span className="cal-num">{day}</span>}
              {day &&
                (byDay[day] || []).map((item) => (
                  <div
                    key={`${item.source || "issue"}-${item.kind}-${item.id}-${itemDate(item)}`}
                    className={`cal-mark is-${item.kind === "plan" ? "plan" : item.kind === "track" ? "track" : "due"} is-${item.source || "issue"}`}
                    style={{ "--mark-color": item.subCategoryColor || item.majorCategoryColor }}
                  >
                    <Link
                      to={markPath(item)}
                      title={markHover(item)}
                      aria-label={markAria(item)}
                      onClick={(event) => event.stopPropagation()}
                    >
                      {markLabel(item)}
                    </Link>
                    <span className="cal-tip">{markHover(item)}</span>
                    {item.kind === "plan" && item.source !== "project" && item.source !== "projectIssue" && item.source !== "workItem" && (
                      <button
                        type="button"
                        className="cal-mark-remove"
                        aria-label={`移出 ${item.title}`}
                        onClick={(event) => {
                          event.preventDefault();
                          event.stopPropagation();
                          onRemovePlan(item);
                        }}
                      >
                        <X size={11} strokeWidth={2} aria-hidden="true" />
                      </button>
                    )}
                  </div>
                ))}
            </div>
          );
        })}
      </div>
    </section>
  );
}
