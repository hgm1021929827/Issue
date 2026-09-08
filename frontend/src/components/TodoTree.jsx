import { useState } from "react";
import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors
} from "@dnd-kit/core";
import { SortableContext, useSortable, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { CheckSquare, GripVertical, Plus, Sparkles, Trash2 } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";

function GripIcon() {
  return <GripVertical size={14} strokeWidth={1.75} aria-hidden="true" />;
}

function PlusIcon() {
  return <Plus size={16} strokeWidth={1.75} aria-hidden="true" />;
}

function TrashIcon() {
  return <Trash2 size={15} strokeWidth={1.75} aria-hidden="true" />;
}

function TodoComposer({ placeholder, autoFocus = false, onAdd, onCancel }) {
  const [title, setTitle] = useState("");

  const submit = async (event) => {
    event.preventDefault();
    const value = title.trim();
    if (!value) return;
    await onAdd(value);
    setTitle("");
  };

  return (
    <form className="todo-composer" onSubmit={submit}>
      <span className="todo-composer-icon">
        <PlusIcon />
      </span>
      <input
        value={title}
        autoFocus={autoFocus}
        placeholder={placeholder}
        onChange={(e) => setTitle(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Escape" && onCancel) onCancel();
        }}
        onBlur={() => {
          if (!title.trim() && onCancel) onCancel();
        }}
      />
    </form>
  );
}

function findNode(nodes, id) {
  for (const node of nodes) {
    if (String(node.id) === String(id)) return node;
    const nested = findNode(node.children || [], id);
    if (nested) return nested;
  }
  return null;
}

function SortableRow({ node, selected, onToggle, onAddStep, onEdit, onDelete }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: node.id });
  const style = { transform: CSS.Transform.toString(transform), transition };
  const steps = node.children?.length || 0;
  const selectedClass = selected ? " is-selected" : "";

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`todo-item${node.isCompleted ? " is-done" : ""}${isDragging ? " is-dragging" : ""}${selectedClass}`}
    >
      <button type="button" className="todo-grip" title="拖曳排序" aria-label="拖曳排序" {...attributes} {...listeners}>
        <GripIcon />
      </button>
      <button
        type="button"
        className={`todo-check${node.isCompleted ? " checked" : ""}`}
        aria-label={node.isCompleted ? "標示未完成" : "標示完成"}
        aria-pressed={node.isCompleted}
        onClick={() => onToggle(node)}
      />
      <button type="button" className="todo-body" onClick={() => onEdit(node)}>
        <span className="todo-title">{node.title}</span>
        {node.content ? <span className="todo-note">{node.content}</span> : null}
        {steps > 0 ? <span className="todo-step-count">{steps} 個步驟</span> : null}
      </button>
      <div className="todo-item-actions">
        <button type="button" className="todo-icon-btn" title="新增步驟" aria-label="新增步驟" onClick={() => onAddStep(node.id)}>
          <PlusIcon />
        </button>
        <button type="button" className="todo-icon-btn danger" title="刪除" aria-label="刪除" onClick={() => onDelete(node)}>
          <TrashIcon />
        </button>
      </div>
    </div>
  );
}

function TodoGroup({
  issueId,
  parentId,
  nodes,
  onChange,
  onError,
  onDialog,
  stepParentId,
  setStepParentId,
  selectedId,
  onSelect
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const ids = nodes.map((n) => n.id);

  const applyTree = (tree) => onChange(tree);

  const toggle = async (node) => {
    try {
      applyTree(await api.completeTodo(node.id, !node.isCompleted));
    } catch (err) {
      onError(err.message);
    }
  };

  const addStep = async (parent, title) => {
    applyTree(await api.createTodo(issueId, { parentId: parent, title, content: "" }));
  };

  const onDragEnd = async (event) => {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    if (!ids.includes(over.id)) {
      onError("本輪僅支援同層排序");
      return;
    }
    const oldIndex = ids.indexOf(active.id);
    const newIndex = ids.indexOf(over.id);
    const orderedIds = arrayMove(ids, oldIndex, newIndex);
    try {
      applyTree(await api.reorderTodos(active.id, { parentId: parentId ?? null, orderedIds }));
    } catch (err) {
      onError(err.message);
    }
  };

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <SortableContext items={ids} strategy={verticalListSortingStrategy}>
        <div className={parentId ? "todo-group nested" : "todo-group"}>
          {nodes.map((node) => (
            <div key={node.id} className="todo-block">
              <SortableRow
                node={node}
                selected={String(selectedId) === String(node.id)}
                onToggle={toggle}
                onAddStep={setStepParentId}
                onEdit={onSelect}
                onDelete={(item) => onDialog({ type: "delete", item })}
              />
              {node.children?.length > 0 && (
                <TodoGroup
                  issueId={issueId}
                  parentId={node.id}
                  nodes={node.children}
                  onChange={onChange}
                  onError={onError}
                  onDialog={onDialog}
                  stepParentId={stepParentId}
                  setStepParentId={setStepParentId}
                  selectedId={selectedId}
                  onSelect={onSelect}
                />
              )}
              {stepParentId === node.id && (
                <TodoComposer
                  autoFocus
                  placeholder="新增步驟"
                  onAdd={async (title) => {
                    try {
                      await addStep(node.id, title);
                      setStepParentId(null);
                    } catch (err) {
                      onError(err.message);
                    }
                  }}
                  onCancel={() => setStepParentId(null)}
                />
              )}
            </div>
          ))}
        </div>
      </SortableContext>
    </DndContext>
  );
}

export default function TodoTree({ issueId, nodes, onChange, onError }) {
  const [dialog, setDialog] = useState(null);
  const [stepParentId, setStepParentId] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [draft, setDraft] = useState({ title: "", content: "" });
  const [saving, setSaving] = useState(false);

  const selected = findNode(nodes, selectedId);

  const openDetail = (item) => {
    setSelectedId(item.id);
    setDraft({ title: item.title || "", content: item.content || "" });
  };

  const saveDetail = async (event) => {
    event.preventDefault();
    if (!selected) return;
    const title = draft.title.trim();
    if (!title) return;
    setSaving(true);
    try {
      const tree = await api.updateTodo(selected.id, { title, content: draft.content.trim() });
      onChange(tree);
      const updated = findNode(tree, selected.id);
      if (updated) {
        setDraft({ title: updated.title || "", content: updated.content || "" });
      }
    } catch (err) {
      onError(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
    <section className="card todo-workspace">
      <div className="todo-list-col">
        <h2>
          <CheckSquare size={18} strokeWidth={1.75} aria-hidden="true" />
          待辦
        </h2>
        <TodoGroup
          issueId={issueId}
          parentId={null}
          nodes={nodes}
          onChange={onChange}
          onError={onError}
          onDialog={setDialog}
          stepParentId={stepParentId}
          setStepParentId={setStepParentId}
          selectedId={selectedId}
          onSelect={openDetail}
        />
        <TodoComposer
          placeholder="新增工作"
          onAdd={async (title) => {
            try {
              const tree = await api.createTodo(issueId, { parentId: null, title, content: "" });
              onChange(tree);
            } catch (err) {
              onError(err.message);
            }
          }}
        />
      </div>
      {selected ? (
        <form className="todo-detail-col" onSubmit={saveDetail}>
          <h3>工作詳細資料</h3>
          <label>
            標題
            <input
              value={draft.title}
              onChange={(e) => setDraft({ ...draft, title: e.target.value })}
              required
            />
          </label>
          <label>
            內容
            <textarea
              rows={8}
              value={draft.content}
              placeholder="寫下這項工作的說明…"
              onChange={(e) => setDraft({ ...draft, content: e.target.value })}
            />
          </label>
          <button className="btn" type="submit" disabled={saving}>
            {saving ? "儲存中…" : "儲存"}
          </button>
        </form>
      ) : (
        <p className="todo-detail-empty">
          <Sparkles size={18} strokeWidth={1.75} aria-hidden="true" />
          點選一項工作，即可查看與儲存內容
        </p>
      )}
    </section>
      <AppDialog
        open={dialog?.type === "delete"}
        title="刪除工作"
        message={`刪除「${dialog?.item?.title || ""}」及其步驟？`}
        confirmLabel="刪除"
        danger
        onCancel={() => setDialog(null)}
        onSubmit={async () => {
          try {
            const deletedId = dialog.item.id;
            const tree = await api.deleteTodo(deletedId);
            onChange(tree);
            if (String(selectedId) === String(deletedId)) {
              setSelectedId(null);
            }
            setDialog(null);
          } catch (err) {
            onError(err.message);
            setDialog(null);
          }
        }}
      />
    </>
  );
}

