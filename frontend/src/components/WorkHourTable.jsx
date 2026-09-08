import { useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";
import AppDateField from "./AppDateField.jsx";

const empty = () => ({ date: "", hours: "", remark: "" });

export default function WorkHourTable({ hours, hoursTotal, onCreate, onUpdate, onDelete, onError }) {
  const [form, setForm] = useState(empty());
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);

  const payload = () => ({
    date: form.date,
    hours: Number(form.hours),
    remark: form.remark
  });

  const submit = async (event) => {
    event.preventDefault();
    setSaving(true);
    try {
      if (editingId) await onUpdate(editingId, payload());
      else await onCreate(payload());
      setForm(empty());
      setEditingId(null);
    } catch (err) {
      onError?.(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="hours-card">
      <div className="row-between">
        <h3>紀錄工時</h3>
        <span className="muted">合計 {Number(hoursTotal || 0).toFixed(2)}</span>
      </div>
      <form className="hours-form" onSubmit={submit}>
        <label>
          日期
          <AppDateField value={form.date} onChange={(date) => setForm({ ...form, date })} required />
        </label>
        <label>
          工時
          <input
            required
            type="number"
            min="0.01"
            max="999.99"
            step="0.01"
            value={form.hours}
            onChange={(e) => setForm({ ...form, hours: e.target.value })}
          />
        </label>
        <label className="hours-remark">
          備註
          <input
            maxLength={2000}
            value={form.remark}
            onChange={(e) => setForm({ ...form, remark: e.target.value })}
          />
        </label>
        <button className="btn" type="submit" disabled={saving}>
          <Plus size={14} strokeWidth={1.75} aria-hidden="true" />
          {editingId ? "更新" : "新增"}
        </button>
        {editingId && (
          <button
            className="btn"
            type="button"
            onClick={() => {
              setEditingId(null);
              setForm(empty());
            }}
          >
            取消
          </button>
        )}
      </form>
      {hours.length === 0 && <p className="muted">尚無工時。</p>}
      {hours.length > 0 && (
        <table className="hours-table">
          <thead>
            <tr>
              <th className="hours-icon-col" />
              <th>日期</th>
              <th>工時</th>
              <th>備註</th>
              <th className="hours-icon-col" />
            </tr>
          </thead>
          <tbody>
            {hours.map((row) => (
              <tr key={row.id}>
                <td className="hours-icon-col">
                  <button
                    type="button"
                    className="todo-icon-btn"
                    title="編輯"
                    aria-label="編輯工時"
                    onClick={() => {
                      setEditingId(row.id);
                      setForm({ date: row.date, hours: String(row.hours), remark: row.remark || "" });
                    }}
                  >
                    <Pencil size={15} strokeWidth={1.75} aria-hidden="true" />
                  </button>
                </td>
                <td>{row.date}</td>
                <td>{row.hours}</td>
                <td>{row.remark || "—"}</td>
                <td className="hours-icon-col">
                  <button
                    type="button"
                    className="todo-icon-btn danger"
                    title="刪除"
                    aria-label="刪除工時"
                    onClick={async () => {
                      try {
                        await onDelete(row.id);
                        if (editingId === row.id) {
                          setEditingId(null);
                          setForm(empty());
                        }
                      } catch (err) {
                        onError?.(err.message);
                      }
                    }}
                  >
                    <Trash2 size={15} strokeWidth={1.75} aria-hidden="true" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
