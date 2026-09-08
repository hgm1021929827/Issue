import { useEffect, useRef, useState } from "react";

export default function AppDialog({
  open,
  title,
  message,
  confirmLabel = "確定",
  cancelLabel = "取消",
  danger = false,
  wide = false,
  fields,
  initial,
  children,
  onCancel,
  onSubmit
}) {
  const ref = useRef(null);
  const [values, setValues] = useState({});

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (open) {
      setValues(initial || {});
      if (!node.open) node.showModal();
    } else if (node.open) {
      node.close();
    }
  }, [open]);

  const closeFromBackdrop = (event) => {
    if (event.target === ref.current) onCancel();
  };

  const submit = (event) => {
    event.preventDefault();
    if (fields?.some((f) => f.required && !(values[f.name] || "").trim())) return;
    onSubmit(fields ? values : true);
  };

  return (
    <dialog ref={ref} className="app-dialog" onCancel={onCancel} onClick={closeFromBackdrop}>
      <form className={`dialog-panel${wide ? " is-wide" : ""}${children ? " form" : ""}`} onSubmit={submit}>
        <h3>{title}</h3>
        {message && <p>{message}</p>}
        {children}
        {fields?.map((field) => (
          <label key={field.name}>
            {field.label}
            {field.multiline ? (
              <textarea
                rows={3}
                value={values[field.name] || ""}
                onChange={(e) => setValues({ ...values, [field.name]: e.target.value })}
              />
            ) : (
              <input
                autoFocus={field.required}
                value={values[field.name] || ""}
                onChange={(e) => setValues({ ...values, [field.name]: e.target.value })}
                required={field.required}
              />
            )}
          </label>
        ))}
        <div className="btn-row dialog-actions">
          {cancelLabel != null && (
            <button type="button" onClick={onCancel}>
              {cancelLabel}
            </button>
          )}
          <button type="submit" className={danger ? "btn danger" : "btn"}>
            {confirmLabel}
          </button>
        </div>
      </form>
    </dialog>
  );
}
