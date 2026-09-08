import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

function menuHost(node) {
  return node?.closest("dialog") || document.body;
}

function menuStyle(trigger, host, optionCount) {
  const t = trigger.getBoundingClientRect();
  const inDialog = host instanceof HTMLDialogElement;
  const frame = inDialog ? host.getBoundingClientRect() : { top: 0, left: 0, bottom: window.innerHeight };
  const estimated = Math.min(260, 12 + optionCount * 44);
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

export default function AppSelect({ value, onChange, options, colored = false, placeholder = "請選擇", disabled = false }) {
  const [open, setOpen] = useState(false);
  const [style, setStyle] = useState(null);
  const rootRef = useRef(null);
  const menuRef = useRef(null);
  const selected = options.find((item) => String(item.value) === String(value));

  useLayoutEffect(() => {
    if (!open) {
      setStyle(null);
      return undefined;
    }
    const trigger = rootRef.current?.querySelector(".app-select-trigger");
    const host = menuHost(rootRef.current);
    if (!trigger || !host) return undefined;
    const update = () => setStyle(menuStyle(trigger, host, options.length));
    update();
    host.addEventListener("scroll", update, true);
    window.addEventListener("resize", update);
    return () => {
      host.removeEventListener("scroll", update, true);
      window.removeEventListener("resize", update);
    };
  }, [open, options.length]);

  useEffect(() => {
    if (!open) return undefined;
    const close = (event) => {
      if (rootRef.current?.contains(event.target) || menuRef.current?.contains(event.target)) return;
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
  }, [open]);

  const menu = open && style && (
    <ul ref={menuRef} className={`app-select-menu is-popover${colored ? " is-color" : ""}`} role="listbox" style={style}>
      {options.map((item) => {
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
                setOpen(false);
              }}
            >
              {item.label}
            </button>
          </li>
        );
      })}
    </ul>
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
          setOpen((prev) => !prev);
        }}
      >
        <span>{selected?.label || placeholder}</span>
      </button>
      {menu && createPortal(menu, menuHost(rootRef.current) || document.body)}
    </div>
  );
}
