import { useEffect, useState } from "react";
import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors
} from "@dnd-kit/core";
import { SortableContext, useSortable, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { CheckSquare, ChevronDown, ChevronRight, Eye, EyeOff, GripVertical, Pencil, Plus, Sparkles, Trash2, X } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";
import AppTextarea from "./AppTextarea.jsx";

function GripIcon() {
  return <GripVertical size={14} strokeWidth={1.75} aria-hidden="true" />;
}

function PlusIcon() {
  return <Plus size={16} strokeWidth={1.75} aria-hidden="true" />;
}

function TrashIcon() {
  return <Trash2 size={15} strokeWidth={1.75} aria-hidden="true" />;
}

function PencilIcon() {
  return <Pencil size={15} strokeWidth={1.75} aria-hidden="true" />;
}

function CloseIcon() {
  return <X size={15} strokeWidth={1.75} aria-hidden="true" />;
}

function TodoComposer({ placeholder, autoFocus = false, onAdd, onCancel }) {
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const submit = async (event) => {
    event.preventDefault();
    const value = title.trim();
    if (!value) return;
    await onAdd(value, content.trim());
    setTitle("");
    setContent("");
  };

  return (
    <form
      className="todo-composer has-fields"
      onSubmit={submit}
      onBlur={(event) => {
        if (event.currentTarget.contains(event.relatedTarget)) return;
        if (!title.trim() && !content.trim() && onCancel) onCancel();
      }}
    >
      <span className="todo-composer-icon">
        <PlusIcon />
      </span>
      <div className="todo-composer-fields">
        <input
          value={title}
          autoFocus={autoFocus}
          placeholder={placeholder}
          onChange={(e) => setTitle(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Escape" && onCancel) onCancel();
          }}
        />
        <AppTextarea
          rows={2}
          maxLength={2000}
          value={content}
          placeholder="內容（選填）"
          onChange={(e) => setContent(e.target.value)}
        />
      </div>
      <button className="btn" type="submit">
        新增
      </button>
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

function findAncestors(nodes, id, path = []) {
  for (const node of nodes) {
    if (String(node.id) === String(id)) return path;
    const nested = findAncestors(node.children || [], id, [...path, node.id]);
    if (nested) return nested;
  }
  return null;
}

function isVisibleNode(node, showAll) {
  if (showAll || !node.isCompleted) return true;
  return (node.children || []).some((child) => isVisibleNode(child, false));
}

function visibleChildren(node, showAll) {
  return (node.children || []).filter((child) => isVisibleNode(child, showAll));
}

function stepCountLabel(node) {
  const total = node.children?.length || 0;
  if (total === 0) return null;
  const open = (node.children || []).filter((child) => !child.isCompleted).length;
  return open === total ? `${total} 個步驟` : `${open} / ${total} 個步驟`;
}

function TodoDetailForm({ compact = false, draft, setDraft, saving, onSubmit, onCancel }) {
  return (
    <form
      className={`todo-detail-col${compact ? " is-inline" : ""}`}
      onSubmit={onSubmit}
      onKeyDown={(event) => {
        if (event.key === "Escape" && onCancel) onCancel();
      }}
    >
      {compact ? null : <h3>工作詳細資料</h3>}
      <label>
        標題
        <input
          value={draft.title}
          autoFocus={compact}
          onChange={(e) => setDraft({ ...draft, title: e.target.value })}
          required
        />
      </label>
      <label>
        內容
        <AppTextarea
          rows={compact ? 4 : 8}
          value={draft.content}
          placeholder="寫下這項工作的說明…"
          onChange={(e) => setDraft({ ...draft, content: e.target.value })}
        />
      </label>
      <div className="btn-row">
        <button className="btn" type="submit" disabled={saving}>
          {saving ? "儲存中…" : "儲存"}
        </button>
      </div>
    </form>
  );
}

function SortableRow({ node, selected, expanded, showAll, hidePreview, inlineEdit, onToggle, onToggleExpand, onAddStep, onEdit, onClose, onDelete }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: node.id });
  const style = { transform: CSS.Transform.toString(transform), transition };
  const steps = visibleChildren(node, showAll).length;
  const stepLabel = stepCountLabel(node);
  const selectedClass = selected ? " is-selected" : "";

  return (
    <div
      id={`todo-${node.id}`}
      ref={setNodeRef}
      style={style}
      className={`todo-item${node.isCompleted ? " is-done" : ""}${isDragging ? " is-dragging" : ""}${selectedClass}`}
    >
      <button type="button" className="todo-grip" title="拖曳排序" aria-label="拖曳排序" {...attributes} {...listeners}>
        <GripIcon />
      </button>
      {steps > 0 ? (
        <button
          type="button"
          className="todo-expand"
          title={expanded ? "收起步驟" : "展開步驟"}
          aria-label={expanded ? "收起步驟" : "展開步驟"}
          aria-expanded={expanded}
          onClick={() => onToggleExpand(node.id)}
        >
          {expanded ? <ChevronDown size={16} strokeWidth={1.75} /> : <ChevronRight size={16} strokeWidth={1.75} />}
        </button>
      ) : (
        <span className="todo-expand-spacer" aria-hidden="true" />
      )}
      <button
        type="button"
        className={`todo-check${node.isCompleted ? " checked" : ""}`}
        aria-label={node.isCompleted ? "標示未完成" : "標示完成"}
        aria-pressed={node.isCompleted}
        onClick={() => onToggle(node)}
      />
      {inlineEdit ? (
        <div className="todo-body">
          <span className="todo-title">{node.title}</span>
          {!hidePreview && node.content ? <span className="todo-note">{node.content}</span> : null}
          {stepLabel ? <span className="todo-step-count">{stepLabel}</span> : null}
        </div>
      ) : (
        <button type="button" className="todo-body" onClick={() => onEdit(node)}>
          <span className="todo-title">{node.title}</span>
          {!hidePreview && node.content ? <span className="todo-note">{node.content}</span> : null}
          {stepLabel ? <span className="todo-step-count">{stepLabel}</span> : null}
        </button>
      )}
      <div className="todo-item-tools">
        {inlineEdit ? (
          selected ? (
            <button type="button" className="todo-icon-btn todo-edit is-on" title="關閉編輯" aria-label="關閉編輯" onClick={onClose}>
              <CloseIcon />
            </button>
          ) : (
            <button type="button" className="todo-icon-btn todo-edit" title="開啟編輯" aria-label="開啟編輯" onClick={() => onEdit(node)}>
              <PencilIcon />
            </button>
          )
        ) : null}
        <div className="todo-item-actions">
          <button type="button" className="todo-icon-btn" title="新增步驟" aria-label="新增步驟" onClick={() => onAddStep(node.id)}>
            <PlusIcon />
          </button>
          <button type="button" className="todo-icon-btn danger" title="刪除" aria-label="刪除" onClick={() => onDelete(node)}>
            <TrashIcon />
          </button>
        </div>
      </div>
    </div>
  );
}

function TodoGroup({
  parentId,
  nodes,
  onChange,
  onError,
  onDialog,
  stepParentId,
  setStepParentId,
  selectedId,
  onSelect,
  create,
  collapsedIds,
  onToggleExpand,
  onExpand,
  showAll,
  renderInlineEditor,
  onClose
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const visibleNodes = showAll ? nodes : nodes.filter((node) => isVisibleNode(node, false));
  const ids = visibleNodes.map((n) => n.id);

  const applyTree = (tree) => onChange(tree);

  const toggle = async (node) => {
    try {
      applyTree(await api.completeTodo(node.id, !node.isCompleted));
    } catch (err) {
      onError(err.message);
    }
  };

  const addStep = async (parent, title, content) => {
    applyTree(await create({ parentId: parent, title, content }));
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
    const visibleQueue = [...arrayMove(ids, oldIndex, newIndex)];
    const orderedIds = showAll
      ? visibleQueue
      : nodes.map((node) => (isVisibleNode(node, false) ? visibleQueue.shift() : node.id));
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
          {visibleNodes.map((node) => (
            <div key={node.id} className="todo-block">
              <SortableRow
                node={node}
                selected={String(selectedId) === String(node.id)}
                expanded={!collapsedIds.has(node.id)}
                showAll={showAll}
                onToggle={toggle}
                onToggleExpand={onToggleExpand}
                onAddStep={(id) => {
                  onExpand(id);
                  setStepParentId(id);
                }}
                hidePreview={Boolean(renderInlineEditor) && String(selectedId) === String(node.id)}
                inlineEdit={Boolean(renderInlineEditor)}
                onEdit={onSelect}
                onClose={onClose}
                onDelete={(item) => onDialog({ type: "delete", item })}
              />
              {renderInlineEditor?.(node)}
              {visibleChildren(node, showAll).length > 0 && !collapsedIds.has(node.id) && (
                <TodoGroup
                  parentId={node.id}
                  nodes={node.children}
                  onChange={onChange}
                  onError={onError}
                  onDialog={onDialog}
                  stepParentId={stepParentId}
                  setStepParentId={setStepParentId}
                  selectedId={selectedId}
                  onSelect={onSelect}
                  create={create}
                  collapsedIds={collapsedIds}
                  onToggleExpand={onToggleExpand}
                  onExpand={onExpand}
                  showAll={showAll}
                  renderInlineEditor={renderInlineEditor}
                  onClose={onClose}
                />
              )}
              {stepParentId === node.id && (
                <TodoComposer
                  autoFocus
                  placeholder="新增步驟"
                  onAdd={async (title, content) => {
                    try {
                      await addStep(node.id, title, content);
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

export default function TodoTree({ nodes, onChange, onError, create, compact = false, highlightId = null }) {
  const [dialog, setDialog] = useState(null);
  const [stepParentId, setStepParentId] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [collapsedIds, setCollapsedIds] = useState(() => new Set());
  const [hideCompleted, setHideCompleted] = useState(false);
  const [draft, setDraft] = useState({ title: "", content: "" });
  const [saving, setSaving] = useState(false);
  const showAll = !hideCompleted;
  const hasVisible = (nodes || []).some((node) => isVisibleNode(node, showAll));

  useEffect(() => {
    if (!highlightId) return;
    const node = findNode(nodes || [], highlightId);
    if (!node) return;
    if (node.isCompleted) setHideCompleted(false);
    const ancestors = findAncestors(nodes || [], highlightId) || [];
    if (ancestors.length > 0) {
      setCollapsedIds((prev) => {
        const next = new Set(prev);
        ancestors.forEach((id) => next.delete(id));
        return next;
      });
    }
    setSelectedId(node.id);
    setDraft({ title: node.title || "", content: node.content || "" });
    const timer = window.setTimeout(() => {
      document.getElementById(`todo-${highlightId}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => window.clearTimeout(timer);
  }, [highlightId, nodes]);

  const toggleExpand = (id) => {
    setCollapsedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const expand = (id) => {
    setCollapsedIds((prev) => {
      if (!prev.has(id)) return prev;
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  };

  const selected = findNode(nodes, selectedId);

  const closeDetail = () => setSelectedId(null);

  const openDetail = (item) => {
    if (String(selectedId) === String(item.id)) return;
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
    <section className={`card todo-workspace${compact ? " is-compact" : ""}`}>
      <div className="todo-list-col">
        <div className="todo-list-head">
          <h2>
            <CheckSquare size={18} strokeWidth={1.75} aria-hidden="true" />
            待辦
          </h2>
          <button
            type="button"
            className={`chip todo-hide-done${hideCompleted ? " active" : ""}`}
            aria-pressed={hideCompleted}
            onClick={() => setHideCompleted((prev) => !prev)}
          >
            {hideCompleted ? <Eye size={14} strokeWidth={1.75} aria-hidden="true" /> : <EyeOff size={14} strokeWidth={1.75} aria-hidden="true" />}
            {hideCompleted ? "顯示已完成" : "隱藏已完成"}
          </button>
        </div>
        {hideCompleted && nodes?.length > 0 && !hasVisible ? (
          <p className="todo-empty-filter">目前沒有未完成待辦，可再按「顯示已完成」查看。</p>
        ) : null}
        <TodoGroup
          parentId={null}
          nodes={nodes}
          onChange={onChange}
          onError={onError}
          onDialog={setDialog}
          stepParentId={stepParentId}
          setStepParentId={setStepParentId}
          selectedId={selectedId}
          onSelect={openDetail}
          create={create}
          collapsedIds={collapsedIds}
          onToggleExpand={toggleExpand}
          onExpand={expand}
          showAll={showAll}
          onClose={closeDetail}
          renderInlineEditor={compact ? (node) => (
            String(selectedId) === String(node.id) ? (
              <TodoDetailForm
                compact
                draft={draft}
                setDraft={setDraft}
                saving={saving}
                onSubmit={saveDetail}
                onCancel={closeDetail}
              />
            ) : null
          ) : undefined}
        />
        <TodoComposer
          placeholder="新增工作"
          onAdd={async (title, content) => {
            try {
              const tree = await create({ parentId: null, title, content });
              onChange(tree);
            } catch (err) {
              onError(err.message);
            }
          }}
        />
      </div>
      {compact ? null : selected ? (
        <TodoDetailForm
          draft={draft}
          setDraft={setDraft}
          saving={saving}
          onSubmit={saveDetail}
        />
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
