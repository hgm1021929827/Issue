import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";

function menuHost(node) {
  return node?.closest("dialog") || document.body;
}

function menuStyle(trigger, host, optionCount, searchable) {
  const t = trigger.getBoundingClientRect();
  const inDialog = host instanceof HTMLDialogElement;
  const frame = inDialog ? host.getBoundingClientRect() : { top: 0, left: 0, bottom: window.innerHeight };
  const list = Math.min(260, 12 + optionCount * 44);
  const estimated = Math.min(320, list + (searchable ? 56 : 0));
  const gap = 6;
  const spaceBelow = (inDialog ? frame.bottom : window.innerHeight) - t.bottom;
  const openUp = spaceBelow < estimated && t.top - frame.top > spaceBelow;
  const top = openUp ? t.top - estimated - gap : t.bottom + gap;
  return {
    position: inDialog ? "absolute" : "fixed",
    top: inDialog ? top - frame.top : top,
    left: inDialog ? t.left - frame.left : t.left,
    width: t.width,
    zIndex: 50
  };
}

export default function AppSelect({
  value,
  onChange,
  options,
  colored = false,
  placeholder = "請選擇",
  disabled = false,
  searchable = false,
  searchPlaceholder = "搜尋"
}) {
  const [open, setOpen] = useState(false);
  const [style, setStyle] = useState(null);
  const [query, setQuery] = useState("");
  const rootRef = useRef(null);
  const menuRef = useRef(null);
  const searchRef = useRef(null);
  const selected = options.find((item) => String(item.value) === String(value));
  const menuReady = Boolean(style);
  const visible = useMemo(() => {
    if (!searchable) return options;
    const key = query.trim().toLowerCase();
    if (!key) return options;
    return options.filter((item) => String(item.label).toLowerCase().includes(key));
  }, [options, query, searchable]);

  const closeMenu = () => {
    setOpen(false);
    setQuery("");
  };

  useLayoutEffect(() => {
    if (!open) {
      setStyle(null);
      return undefined;
    }
    const trigger = rootRef.current?.querySelector(".app-select-trigger");
    const host = menuHost(rootRef.current);
    if (!trigger || !host) return undefined;
    const update = () => setStyle(menuStyle(trigger, host, visible.length, searchable));
    update();
    host.addEventListener("scroll", update, true);
    window.addEventListener("resize", update);
    return () => {
      host.removeEventListener("scroll", update, true);
      window.removeEventListener("resize", update);
    };
  }, [open, visible.length, searchable]);

  useEffect(() => {
    if (!open) return undefined;
    const close = (event) => {
      if (rootRef.current?.contains(event.target) || menuRef.current?.contains(event.target)) return;
      closeMenu();
    };
    const onKey = (event) => {
      if (event.key !== "Escape") return;
      event.stopPropagation();
      closeMenu();
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open]);

  useEffect(() => {
    if (!open || !searchable || !menuReady) return;
    searchRef.current?.focus();
  }, [open, searchable, menuReady]);

  const optionList = visible.map((item) => {
    const active = String(item.value) === String(value);
    return (
      <li key={String(item.value)}>
        <button
          type="button"
          role="option"
          aria-selected={active}
          className={active ? "is-selected" : ""}
          style={colored && item.color ? { "--option-color": item.color } : undefined}
          onClick={() => {
            onChange(item.value);
            closeMenu();
          }}
        >
          {item.label}
        </button>
      </li>
    );
  });

  const emptyItem = searchable && visible.length === 0 ? (
    <li className="app-select-empty muted">沒有符合的項目</li>
  ) : null;

  const menu = open && style && (
    searchable ? (
      <div
        ref={menuRef}
        className={`app-select-menu is-popover is-searchable${colored ? " is-color" : ""}`}
        style={style}
      >
        <div className="app-select-search">
          <input
            ref={searchRef}
            type="search"
            value={query}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={(event) => event.stopPropagation()}
          />
        </div>
        <ul className="app-select-options" role="listbox">
          {optionList}
          {emptyItem}
        </ul>
      </div>
    ) : (
      <ul ref={menuRef} className={`app-select-menu is-popover${colored ? " is-color" : ""}`} role="listbox" style={style}>
        {optionList}
      </ul>
    )
  );

  return (
    <div ref={rootRef} className={`app-select${colored ? " is-color" : ""}${open ? " is-open" : ""}${disabled ? " is-disabled" : ""}`}>
      <button
        type="button"
        className="app-select-trigger"
        aria-haspopup="listbox"
        aria-expanded={open}
        disabled={disabled}
        style={colored && selected?.color ? { "--select-color": selected.color } : undefined}
        onClick={() => {
          if (disabled) return;
          setOpen((prev) => {
            if (prev) setQuery("");
            return !prev;
          });
        }}
      >
        <span>{selected?.label || placeholder}</span>
      </button>
      {menu && createPortal(menu, menuHost(rootRef.current) || document.body)}
    </div>
  );
}
