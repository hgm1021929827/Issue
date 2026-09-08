import { useEffect, useLayoutEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";

function pad(n) {
  return String(n).padStart(2, "0");
}

function toIso(year, month, day) {
  return `${year}-${pad(month)}-${pad(day)}`;
}

function parseIso(iso) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || "");
  if (!match) return null;
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null;
  return { year, month, day };
}

function daysInMonth(year, month) {
  return new Date(year, month, 0).getDate();
}

function todayIso() {
  const now = new Date();
  return toIso(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

function monthCells(year, month) {
  const firstWeekday = new Date(year, month - 1, 1).getDay();
  const total = daysInMonth(year, month);
  const cells = [];
  for (let i = 0; i < firstWeekday; i += 1) cells.push(null);
  for (let d = 1; d <= total; d += 1) cells.push(d);
  while (cells.length % 7 !== 0) cells.push(null);
  return cells;
}

function menuHost(node) {
  return node?.closest("dialog") || document.body;
}

function panelStyle(trigger, host) {
  const t = trigger.getBoundingClientRect();
  const inDialog = host instanceof HTMLDialogElement;
  const frame = inDialog
    ? host.getBoundingClientRect()
    : { top: 0, left: 0, bottom: window.innerHeight, right: window.innerWidth };
  const width = 288;
  const estimated = 352;
  const gap = 6;
  const spaceBelow = (inDialog ? frame.bottom : window.innerHeight) - t.bottom;
  const openUp = spaceBelow < estimated && t.top - frame.top > spaceBelow;
  const top = openUp ? t.top - estimated - gap : t.bottom + gap;
  const maxRight = inDialog ? frame.right : window.innerWidth;
  const minLeft = inDialog ? frame.left : 0;
  let left = t.left;
  if (left + width > maxRight - 8) left = Math.max(minLeft + 8, maxRight - width - 8);
  return {
    position: inDialog ? "absolute" : "fixed",
    top: inDialog ? top - frame.top : top,
    left: inDialog ? left - frame.left : left,
    width,
    zIndex: 50
  };
}

export default function AppDateField({
  value = "",
  onChange,
  placeholder = "選擇日期",
  allowClear = true,
  compact = false,
  disabled = false
}) {
  const [open, setOpen] = useState(false);
  const [style, setStyle] = useState(null);
  const [root, setRoot] = useState(null);
  const [panel, setPanel] = useState(null);
  const initialView = parseIso(value) || parseIso(todayIso());
  const [viewYear, setViewYear] = useState(initialView.year);
  const [viewMonth, setViewMonth] = useState(initialView.month);

  useEffect(() => {
    if (!open) return;
    const next = parseIso(value) || parseIso(todayIso());
    setViewYear(next.year);
    setViewMonth(next.month);
  }, [open, value]);

  useLayoutEffect(() => {
    if (!open) {
      setStyle(null);
      return undefined;
    }
    const trigger = root?.querySelector(".app-date-trigger");
    const host = menuHost(root);
    if (!trigger || !host) return undefined;
    const update = () => setStyle(panelStyle(trigger, host));
    update();
    host.addEventListener("scroll", update, true);
    window.addEventListener("resize", update);
    return () => {
      host.removeEventListener("scroll", update, true);
      window.removeEventListener("resize", update);
    };
  }, [open, root]);

  useEffect(() => {
    if (!open) return undefined;
    const close = (event) => {
      if (root?.contains(event.target) || panel?.contains(event.target)) return;
      setOpen(false);
    };
    const onKey = (event) => {
      if (event.key !== "Escape") return;
      event.stopPropagation();
      setOpen(false);
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open, root, panel]);

  const cells = useMemo(() => monthCells(viewYear, viewMonth), [viewYear, viewMonth]);
  const today = parseIso(todayIso());

  const shiftMonth = (delta) => {
    const date = new Date(viewYear, viewMonth - 1 + delta, 1);
    setViewYear(date.getFullYear());
    setViewMonth(date.getMonth() + 1);
  };

  const pickIso = (iso) => {
    onChange(iso);
    setOpen(false);
  };

  const popover = open && style && (
    <>
      <div className="app-date-backdrop" aria-hidden="true" />
      <div ref={setPanel} className="app-date-panel" role="dialog" aria-label="選擇日期" style={style}>
        <div className="app-date-head">
          <button type="button" className="app-date-nav" aria-label="上月" onClick={() => shiftMonth(-1)}>
            <ChevronLeft size={16} strokeWidth={1.75} aria-hidden="true" />
          </button>
          <strong>
            {viewYear} 年 {viewMonth} 月
          </strong>
          <button type="button" className="app-date-nav" aria-label="下月" onClick={() => shiftMonth(1)}>
            <ChevronRight size={16} strokeWidth={1.75} aria-hidden="true" />
          </button>
        </div>
        <div className="app-date-grid is-head">
          {["日", "一", "二", "三", "四", "五", "六"].map((d) => (
            <span key={d}>{d}</span>
          ))}
        </div>
        <div className="app-date-grid">
          {cells.map((day, index) => {
            if (!day) return <span key={index} className="app-date-empty" />;
            const iso = toIso(viewYear, viewMonth, day);
            const isSelected = value === iso;
            const isToday = today && today.year === viewYear && today.month === viewMonth && today.day === day;
            return (
              <button
                key={iso}
                type="button"
                className={`app-date-day${isSelected ? " is-selected" : ""}${isToday ? " is-today" : ""}`}
                onClick={() => pickIso(iso)}
              >
                {day}
              </button>
            );
          })}
        </div>
        <div className="app-date-actions">
          <button type="button" onClick={() => pickIso(todayIso())}>
            今天
          </button>
          {allowClear && (
            <button type="button" onClick={() => pickIso("")}>
              清除
            </button>
          )}
        </div>
      </div>
    </>
  );

  return (
    <div ref={setRoot} className={`app-date${compact ? " is-compact" : ""}${open ? " is-open" : ""}${disabled ? " is-disabled" : ""}`}>
      <button
        type="button"
        className="app-date-trigger"
        aria-haspopup="dialog"
        aria-expanded={open}
        disabled={disabled}
        onClick={() => {
          if (disabled) return;
          setOpen((prev) => !prev);
        }}
      >
        <span className={value ? "" : "is-placeholder"}>{value || placeholder}</span>
        <CalendarDays size={compact ? 15 : 16} strokeWidth={1.75} aria-hidden="true" />
      </button>
      {popover && createPortal(popover, menuHost(root) || document.body)}
    </div>
  );
}
