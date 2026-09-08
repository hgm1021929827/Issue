import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors
} from "@dnd-kit/core";
import { SortableContext, useSortable, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { BookmarkPlus, GripVertical, Plus, Sparkles, Trash2 } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";
import AppSelect from "./AppSelect.jsx";
import AppDateField from "./AppDateField.jsx";
import PageTitle from "./PageTitle.jsx";

function targetLabel(item) {
  if (item.targetType === "member") {
    return `${item.targetName}（${item.targetSecondaryName}）`;
  }
  return item.targetSecondaryName ? `${item.targetName}（${item.targetSecondaryName}）` : item.targetName;
}

function emptyForm() {
  return { title: "", content: "", targetType: "member", targetId: "", reminderDate: "" };
}

function SortableTrackRow({ item, highlight, onToggle, onEdit, onDelete }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.id });
  const style = { transform: CSS.Transform.toString(transform), transition };
  return (
    <div
      id={`track-todo-${item.id}`}
      ref={setNodeRef}
      style={style}
      className={`todo-item${item.isCompleted ? " is-done" : ""}${isDragging ? " is-dragging" : ""}${highlight ? " is-flash" : ""}`}
    >
      <button type="button" className="todo-grip" title="拖曳排序" aria-label="拖曳排序" {...attributes} {...listeners}>
        <GripVertical size={14} strokeWidth={1.75} aria-hidden="true" />
      </button>
      <button
        type="button"
        className={`todo-check${item.isCompleted ? " checked" : ""}`}
        aria-label={item.isCompleted ? "標示未完成" : "標示完成"}
        aria-pressed={item.isCompleted}
        onClick={() => onToggle(item)}
      />
      <button type="button" className="todo-body" onClick={() => onEdit(item)}>
        <span className="todo-title">{item.title}</span>
        <span className="todo-note">{targetLabel(item)}</span>
        {item.reminderDate ? <span className="todo-note">提醒日 {item.reminderDate}</span> : null}
        {item.content ? <span className="todo-note">{item.content}</span> : null}
      </button>
      <div className="todo-item-actions">
        <button type="button" className="todo-icon-btn danger" title="刪除" aria-label="刪除" onClick={() => onDelete(item)}>
          <Trash2 size={15} strokeWidth={1.75} aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}

export default function TrackTodoList({
  items,
  onChange,
  onError,
  vendorCompanyId,
  create,
  highlightId,
  compact = false,
  ready = true
}) {
  const [form, setForm] = useState(emptyForm());
  const [editingId, setEditingId] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [members, setMembers] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [hint, setHint] = useState("");
  const [askDelete, setAskDelete] = useState(null);
  const [missing, setMissing] = useState(false);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  useEffect(() => {
    api.companyMembers().then(setMembers).catch((err) => onError(err.message));
  }, []);

  useEffect(() => {
    if (!vendorCompanyId) {
      setContacts([]);
      return;
    }
    api.clientContacts(vendorCompanyId).then(setContacts).catch((err) => onError(err.message));
  }, [vendorCompanyId]);

  useEffect(() => {
    if (!highlightId) {
      setMissing(false);
      return;
    }
    if (!ready) {
      setMissing(false);
      return;
    }
    const found = items.some((x) => String(x.id) === String(highlightId));
    setMissing(!found);
    if (!found) return;
    const timer = window.setTimeout(() => {
      document.getElementById(`track-todo-${highlightId}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => window.clearTimeout(timer);
  }, [highlightId, items, ready]);

  useEffect(() => {
    if (!showForm) return;
    refreshHint(form.targetType, form.targetId);
  }, [members, contacts, vendorCompanyId, showForm, form.targetType, form.targetId]);

  const refreshHint = (type, targetId) => {
    if (type === "member") {
      setHint(members.length === 0 ? "請先到成員頁新增公司成員" : "");
      return;
    }
    if (!vendorCompanyId) {
      setHint("請先設定廠商");
      return;
    }
    if (contacts.length === 0) {
      setHint("請先在成員維護新增客戶窗口");
      return;
    }
    setHint(targetId ? "" : "");
  };

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm());
    setShowForm(true);
    refreshHint("member", "");
  };

  const openEdit = (item) => {
    setEditingId(item.id);
    setForm({
      title: item.title,
      content: item.content || "",
      targetType: item.targetType,
      targetId: item.targetId,
      reminderDate: item.reminderDate || ""
    });
    setShowForm(true);
    refreshHint(item.targetType, item.targetId);
  };

  const payload = () => ({
    title: form.title,
    content: form.content,
    targetType: form.targetType,
    targetId: form.targetId === "" ? null : Number(form.targetId),
    reminderDate: form.reminderDate || null
  });

  const save = async (event) => {
    event?.preventDefault();
    event?.stopPropagation();
    if (!form.title.trim()) {
      onError("請填寫標題");
      return;
    }
    if (form.targetId === "" || form.targetId == null) {
      onError("請選擇追蹤對象");
      return;
    }
    if (form.targetType === "clientContact" && !vendorCompanyId) {
      setHint("請先設定廠商");
      return;
    }
    try {
      const list = editingId
        ? await api.updateTrackTodo(editingId, payload())
        : await create(payload());
      onChange(list);
      setShowForm(false);
      setEditingId(null);
      onError("");
    } catch (err) {
      onError(err.message);
    }
  };

  const toggle = async (item) => {
    try {
      onChange(await api.completeTrackTodo(item.id, !item.isCompleted));
      onError("");
    } catch (err) {
      onError(err.message);
    }
  };

  const onDragEnd = async (event) => {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    const oldIndex = items.findIndex((x) => String(x.id) === String(active.id));
    const newIndex = items.findIndex((x) => String(x.id) === String(over.id));
    if (oldIndex < 0 || newIndex < 0) return;
    const next = arrayMove(items, oldIndex, newIndex);
    onChange(next);
    try {
      onChange(await api.reorderTrackTodos(active.id, next.map((x) => x.id)));
      onError("");
    } catch (err) {
      onError(err.message);
      onChange(items);
    }
  };

  const targetOptions = form.targetType === "member"
    ? members.map((m) => ({ value: m.id, label: `${m.name}（${m.englishName}）` }))
    : contacts.map((c) => ({ value: c.id, label: c.name }));

  const onEditorKeyDown = (event) => {
    if (event.key !== "Enter" || event.target.tagName === "TEXTAREA") return;
    event.preventDefault();
    event.stopPropagation();
    save(event);
  };

  return (
    <section
      className={`card track-todo-card${compact ? " is-compact" : ""}`}
      onKeyDown={onEditorKeyDown}
    >
      <div className="row-between">
        <PageTitle icon={BookmarkPlus} as={compact ? "h3" : "h2"}>
          需要追蹤的 TODO
        </PageTitle>
        <button className="btn" type="button" onClick={openCreate}>
          <Plus size={16} strokeWidth={1.75} aria-hidden="true" />
          新增
        </button>
      </div>
      {missing && <p className="banner error">找不到該筆需要追蹤的 TODO</p>}
      {items.length === 0 && !showForm && (
        <div className="empty">
          <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
          <p>尚無需要追蹤的 TODO。</p>
        </div>
      )}
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={items.map((x) => x.id)} strategy={verticalListSortingStrategy}>
          <div className="todo-list">
            {items.map((item) => (
              <SortableTrackRow
                key={item.id}
                item={item}
                highlight={String(highlightId) === String(item.id)}
                onToggle={toggle}
                onEdit={openEdit}
                onDelete={setAskDelete}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>
      {showForm && (
        <div className="form track-todo-form">
          <label>
            標題
            <input
              required
              maxLength={200}
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
          </label>
          <label>
            內容
            <textarea rows={2} maxLength={2000} value={form.content} onChange={(e) => setForm({ ...form, content: e.target.value })} />
          </label>
          <div className="two-col">
            <label>
              對象類型
              <AppSelect
                value={form.targetType}
                options={[
                  { value: "member", label: "內部成員" },
                  { value: "clientContact", label: "客戶窗口" }
                ]}
                onChange={(targetType) => {
                  setForm({ ...form, targetType, targetId: "" });
                  refreshHint(targetType, "");
                }}
              />
            </label>
            <label>
              追蹤對象
              <AppSelect
                value={form.targetId}
                placeholder="請選擇"
                options={[{ value: "", label: "請選擇" }, ...targetOptions]}
                onChange={(targetId) => {
                  setForm({ ...form, targetId });
                  refreshHint(form.targetType, targetId);
                }}
              />
            </label>
          </div>
          <label>
            提醒日
            <AppDateField value={form.reminderDate} onChange={(reminderDate) => setForm({ ...form, reminderDate })} />
          </label>
          {hint && <p className="muted">{hint}</p>}
          <div className="btn-row">
            <button className="btn" type="button" onClick={save}>
              儲存
            </button>
            <button
              className="btn"
              type="button"
              onClick={() => {
                setShowForm(false);
                setEditingId(null);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      {createPortal(
        <AppDialog
          open={Boolean(askDelete)}
          title="刪除需要追蹤的 TODO"
          message={askDelete ? `確定刪除「${askDelete.title}」？` : ""}
          confirmLabel="刪除"
          danger
          onCancel={() => setAskDelete(null)}
          onSubmit={async () => {
            try {
              onChange(await api.deleteTrackTodo(askDelete.id));
              setAskDelete(null);
              onError("");
            } catch (err) {
              onError(err.message);
              setAskDelete(null);
            }
          }}
        />,
        document.body
      )}
    </section>
  );
}
