import { useEffect, useState } from "react";
import { Palette } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "../components/AppDialog.jsx";
import PageTitle from "../components/PageTitle.jsx";

export default function CategoryPage() {
  const [majors, setMajors] = useState([]);
  const [subs, setSubs] = useState([]);
  const [presets, setPresets] = useState([]);
  const [names, setNames] = useState({});
  const [newMajor, setNewMajor] = useState("");
  const [error, setError] = useState("");
  const [dialog, setDialog] = useState(null);

  const load = async () => {
    const [majorList, subList, colors] = await Promise.all([
      api.majorCategories(),
      api.subCategories(),
      api.colorPresets()
    ]);
    setMajors(majorList);
    setSubs(subList);
    setPresets(colors);
  };

  useEffect(() => {
    load().catch((err) => setError(err.message));
  }, []);

  const grouped = (majorId) => subs.filter((s) => s.majorCategoryId === majorId);

  const replaceMajorSubs = (majorId, list) => {
    setSubs((prev) => [...prev.filter((s) => s.majorCategoryId !== majorId), ...list]);
  };

  const add = async (majorId) => {
    const name = (names[majorId] || "").trim();
    if (!name) return;
    try {
      replaceMajorSubs(majorId, await api.createSub({ majorCategoryId: majorId, name }));
      setNames({ ...names, [majorId]: "" });
      setError("");
    } catch (err) {
      setError(err.message);
    }
  };

  const addMajor = async () => {
    const name = newMajor.trim();
    if (!name) return;
    try {
      setMajors(await api.createMajor({ name }));
      setNewMajor("");
      setError("");
    } catch (err) {
      setError(err.message);
    }
  };

  const pickColor = async (item, color) => {
    try {
      replaceMajorSubs(item.majorCategoryId, await api.updateSub(item.id, { color }));
      setError("");
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div>
      <PageTitle icon={Palette}>分類設定</PageTitle>
      <p className="muted">可新增大分類、改名與刪除。顏色只套在小分類，可從 10 個備選色挑選。</p>
      {error && <p className="banner error">{error}</p>}
      <div className="btn-row cat-major-add">
        <input
          placeholder="新大分類名稱"
          value={newMajor}
          onChange={(e) => setNewMajor(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") addMajor();
          }}
        />
        <button type="button" className="btn btn-add" onClick={addMajor}>
          新增大分類
        </button>
      </div>
      <div className="cat-grid">
        {majors.map((m) => (
          <section key={m.id} className="card cat-card">
            <div className="row-between cat-major-head">
              <h2>{m.name}</h2>
              <span>
                <button type="button" onClick={() => setDialog({ type: "renameMajor", item: m })}>
                  改名
                </button>
                <button type="button" onClick={() => setDialog({ type: "deleteMajor", item: m })}>
                  刪除
                </button>
              </span>
            </div>
            {grouped(m.id).length === 0 && <p className="muted">僅空白選項</p>}
            <ul>
              {grouped(m.id).map((s) => (
                <li key={s.id} className="sub-item">
                  <div className="row-between">
                    <span>
                      <span className="swatch" style={{ "--chip-color": s.color }} />
                      {s.name}
                    </span>
                    <span>
                      <button type="button" onClick={() => setDialog({ type: "rename", item: s })}>
                        改名
                      </button>
                      <button type="button" onClick={() => setDialog({ type: "deleteSub", item: s })}>
                        刪除
                      </button>
                    </span>
                  </div>
                  <div className="color-row">
                    {presets.map((color) => (
                      <button
                        key={color}
                        type="button"
                        title={color}
                        className={(s.color || "").toUpperCase() === color.toUpperCase() ? "color-dot selected" : "color-dot"}
                        style={{ "--chip-color": color }}
                        onClick={() => pickColor(s, color)}
                      />
                    ))}
                  </div>
                </li>
              ))}
            </ul>
            <div className="btn-row">
              <input
                placeholder="新小分類名稱"
                value={names[m.id] || ""}
                onChange={(e) => setNames({ ...names, [m.id]: e.target.value })}
              />
              <button type="button" className="btn btn-add" onClick={() => add(m.id)}>
                新增
              </button>
            </div>
          </section>
        ))}
      </div>
      <AppDialog
        open={dialog?.type === "renameMajor"}
        title="修改大分類名稱"
        fields={[{ name: "name", label: "名稱", required: true }]}
        initial={{ name: dialog?.item?.name || "" }}
        onCancel={() => setDialog(null)}
        onSubmit={async (values) => {
          try {
            setMajors(await api.updateMajor(dialog.item.id, { name: values.name }));
            setDialog(null);
            setError("");
          } catch (err) {
            setError(err.message);
            setDialog(null);
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "deleteMajor"}
        title="刪除大分類"
        message={`確定刪除「${dialog?.item?.name || ""}」？底下尚未使用的小分類會一併刪除。`}
        confirmLabel="刪除"
        danger
        onCancel={() => setDialog(null)}
        onSubmit={async () => {
          try {
            const item = dialog.item;
            setMajors(await api.deleteMajor(item.id));
            setSubs((prev) => prev.filter((s) => s.majorCategoryId !== item.id));
            setDialog(null);
            setError("");
          } catch (err) {
            setError(err.message);
            setDialog(null);
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "rename"}
        title="修改小分類名稱"
        fields={[{ name: "name", label: "名稱", required: true }]}
        initial={{ name: dialog?.item?.name || "" }}
        onCancel={() => setDialog(null)}
        onSubmit={async (values) => {
          try {
            const item = dialog.item;
            replaceMajorSubs(item.majorCategoryId, await api.updateSub(item.id, { name: values.name }));
            setDialog(null);
            setError("");
          } catch (err) {
            setError(err.message);
            setDialog(null);
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "deleteSub"}
        title="刪除小分類"
        message={`確定刪除「${dialog?.item?.name || ""}」？`}
        confirmLabel="刪除"
        danger
        onCancel={() => setDialog(null)}
        onSubmit={async () => {
          try {
            const item = dialog.item;
            replaceMajorSubs(item.majorCategoryId, await api.deleteSub(item.id));
            setDialog(null);
            setError("");
          } catch (err) {
            setError(err.message);
            setDialog(null);
          }
        }}
      />
    </div>
  );
}
