import { useEffect, useState } from "react";
import { Building2, Users } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "../components/AppDialog.jsx";
import AppSelect from "../components/AppSelect.jsx";
import PageTitle from "../components/PageTitle.jsx";

const CHANNEL_TYPES = [
  { value: "phone", label: "市話" },
  { value: "mobile", label: "手機" },
  { value: "email", label: "Email" },
  { value: "teams", label: "Teams" },
  { value: "other", label: "其他" }
];

function channelLabel(type) {
  return CHANNEL_TYPES.find((x) => x.value === type)?.label || type;
}

export default function MembersPage() {
  const [tab, setTab] = useState("members");
  const [q, setQ] = useState("");
  const [members, setMembers] = useState([]);
  const [tree, setTree] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState(null);
  const [channelForm, setChannelForm] = useState({ type: "phone", value: "" });

  const load = async (keyword = q) => {
    setLoading(true);
    setError("");
    try {
      if (tab === "members") {
        setMembers(await api.companyMembers(keyword.trim() || undefined));
      } else {
        setTree(await api.clientCompanyTree(keyword.trim() || undefined));
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [tab]);

  const replaceContacts = (companyId, contacts) => {
    setTree((prev) => prev.map((c) => (c.id === companyId ? { ...c, contacts } : c)));
  };

  const replaceChannels = (companyId, contactId, channels) => {
    setTree((prev) =>
      prev.map((c) =>
        c.id === companyId
          ? {
              ...c,
              contacts: c.contacts.map((x) => (x.id === contactId ? { ...x, channels } : x))
            }
          : c
      )
    );
  };

  const closeDialog = () => {
    setDialog(null);
    setChannelForm({ type: "phone", value: "" });
  };

  return (
    <div>
      <PageTitle icon={Users}>成員</PageTitle>
      <p className="muted">維護公司成員與客戶公司窗口。專案可選負責人；專案與正式議題儲存時需選廠商。</p>
      {error && <p className="banner error">{error}</p>}
      <div className="filters member-tabs">
        <button className={tab === "members" ? "chip active" : "chip"} type="button" onClick={() => setTab("members")}>
          公司成員
        </button>
        <button className={tab === "clients" ? "chip active" : "chip"} type="button" onClick={() => setTab("clients")}>
          客戶窗口
        </button>
      </div>
      <div className="btn-row member-search">
        <input
          placeholder={tab === "members" ? "搜尋中文名或英文名" : "搜尋公司或窗口名稱"}
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") load();
          }}
        />
        <button type="button" className="btn" onClick={() => load()}>
          搜尋
        </button>
        {tab === "members" ? (
          <button type="button" className="btn btn-add" onClick={() => setDialog({ type: "member" })}>
            新增成員
          </button>
        ) : (
          <button type="button" className="btn btn-add" onClick={() => setDialog({ type: "company" })}>
            新增客戶公司
          </button>
        )}
      </div>

      {loading && <p className="muted">載入中…</p>}

      {!loading && tab === "members" && members.length === 0 && <p className="muted">尚無公司成員。</p>}
      {!loading && tab === "members" && (
        <ul className="member-list">
          {members.map((item) => (
            <li key={item.id} className="card member-card">
              <div className="row-between">
                <div>
                  <strong>
                    {item.name}
                    <span className="muted">（{item.englishName}）</span>
                  </strong>
                  {item.remark ? <p className="muted member-remark">{item.remark}</p> : null}
                </div>
                <span>
                  <button type="button" onClick={() => setDialog({ type: "member", item })}>
                    編輯
                  </button>
                  <button type="button" onClick={() => setDialog({ type: "deleteMember", item })}>
                    刪除
                  </button>
                </span>
              </div>
            </li>
          ))}
        </ul>
      )}

      {!loading && tab === "clients" && tree.length === 0 && <p className="muted">尚無客戶公司。</p>}
      {!loading && tab === "clients" && (
        <div className="client-tree">
          {tree.map((company) => (
            <section key={company.id} className="card member-card">
              <div className="row-between">
                <h2>
                  <Building2 size={16} strokeWidth={1.75} aria-hidden="true" />
                  {company.name}
                </h2>
                <span>
                  <button type="button" onClick={() => setDialog({ type: "company", item: company })}>
                    改名
                  </button>
                  <button type="button" onClick={() => setDialog({ type: "deleteCompany", item: company })}>
                    刪除
                  </button>
                  <button type="button" className="btn btn-add" onClick={() => setDialog({ type: "contact", company })}>
                    新增窗口
                  </button>
                </span>
              </div>
              {(company.contacts || []).length === 0 && <p className="muted">尚無窗口。</p>}
              <ul className="contact-list">
                {(company.contacts || []).map((contact) => (
                  <li key={contact.id}>
                    <div className="row-between">
                      <strong>
                        {contact.name}
                        {contact.title ? <span className="muted">　{contact.title}</span> : null}
                      </strong>
                      <span>
                        <button type="button" onClick={() => setDialog({ type: "contact", company, item: contact })}>
                          編輯
                        </button>
                        <button type="button" onClick={() => setDialog({ type: "deleteContact", company, item: contact })}>
                          刪除
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            setChannelForm({ type: "phone", value: "" });
                            setDialog({ type: "channel", company, contact });
                          }}
                        >
                          新增聯絡
                        </button>
                      </span>
                    </div>
                    <ul className="channel-list">
                      {(contact.channels || []).map((ch) => (
                        <li key={ch.id} className="row-between">
                          <span>
                            {channelLabel(ch.type)}　{ch.value}
                          </span>
                          <span>
                            <button
                              type="button"
                              onClick={() => {
                                setChannelForm({ type: ch.type, value: ch.value });
                                setDialog({ type: "channel", company, contact, item: ch });
                              }}
                            >
                              編輯
                            </button>
                            <button
                              type="button"
                              onClick={() => setDialog({ type: "deleteChannel", company, contact, item: ch })}
                            >
                              刪除
                            </button>
                          </span>
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}

      <AppDialog
        open={dialog?.type === "member"}
        title={dialog?.item ? "編輯公司成員" : "新增公司成員"}
        confirmLabel="儲存"
        fields={[
          { name: "name", label: "中文名", required: true },
          { name: "englishName", label: "英文名", required: true },
          { name: "remark", label: "備註", multiline: true }
        ]}
        initial={{
          name: dialog?.item?.name || "",
          englishName: dialog?.item?.englishName || "",
          remark: dialog?.item?.remark || ""
        }}
        onCancel={closeDialog}
        onSubmit={async (values) => {
          try {
            if (dialog.item) {
              await api.updateCompanyMember(dialog.item.id, values);
            } else {
              await api.createCompanyMember(values);
            }
            closeDialog();
            await load();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "company"}
        title={dialog?.item ? "修改客戶公司名稱" : "新增客戶公司"}
        confirmLabel="儲存"
        fields={[{ name: "name", label: "客戶公司名稱", required: true }]}
        initial={{ name: dialog?.item?.name || "" }}
        onCancel={closeDialog}
        onSubmit={async (values) => {
          try {
            if (dialog.item) {
              await api.updateClientCompany(dialog.item.id, values);
            } else {
              await api.createClientCompany(values);
            }
            closeDialog();
            await load();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "contact"}
        title={dialog?.item ? "編輯客戶窗口" : "新增客戶窗口"}
        confirmLabel="儲存"
        fields={[
          { name: "name", label: "窗口名稱", required: true },
          { name: "title", label: "職位" }
        ]}
        initial={{ name: dialog?.item?.name || "", title: dialog?.item?.title || "" }}
        onCancel={closeDialog}
        onSubmit={async (values) => {
          try {
            const contacts = dialog.item
              ? await api.updateClientContact(dialog.company.id, dialog.item.id, values)
              : await api.createClientContact(dialog.company.id, values);
            replaceContacts(dialog.company.id, contacts);
            closeDialog();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "channel"}
        title={dialog?.item ? "編輯聯絡資料" : "新增聯絡資料"}
        confirmLabel="儲存"
        onCancel={closeDialog}
        onSubmit={async () => {
          try {
            const payload = { type: channelForm.type, value: channelForm.value };
            const channels = dialog.item
              ? await api.updateContactChannel(dialog.company.id, dialog.contact.id, dialog.item.id, payload)
              : await api.createContactChannel(dialog.company.id, dialog.contact.id, payload);
            replaceChannels(dialog.company.id, dialog.contact.id, channels);
            closeDialog();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      >
        <label>
          聯絡類型
          <AppSelect
            value={channelForm.type}
            options={CHANNEL_TYPES}
            onChange={(type) => setChannelForm({ ...channelForm, type })}
          />
        </label>
        <label>
          聯絡內容
          <input
            required
            maxLength={200}
            value={channelForm.value}
            onChange={(e) => setChannelForm({ ...channelForm, value: e.target.value })}
          />
        </label>
      </AppDialog>
      <AppDialog
        open={dialog?.type === "deleteMember"}
        title="刪除公司成員"
        message={dialog?.item ? `確定刪除「${dialog.item.name}（${dialog.item.englishName}）」？` : ""}
        confirmLabel="刪除"
        danger
        onCancel={closeDialog}
        onSubmit={async () => {
          try {
            await api.deleteCompanyMember(dialog.item.id);
            closeDialog();
            await load();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "deleteCompany"}
        title="刪除客戶公司"
        message={dialog?.item ? `確定刪除「${dialog.item.name}」及其窗口與聯絡資料？` : ""}
        confirmLabel="刪除"
        danger
        onCancel={closeDialog}
        onSubmit={async () => {
          try {
            await api.deleteClientCompany(dialog.item.id);
            closeDialog();
            await load();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "deleteContact"}
        title="刪除客戶窗口"
        message={dialog?.item ? `確定刪除窗口「${dialog.item.name}」？` : ""}
        confirmLabel="刪除"
        danger
        onCancel={closeDialog}
        onSubmit={async () => {
          try {
            const contacts = await api.deleteClientContact(dialog.company.id, dialog.item.id);
            replaceContacts(dialog.company.id, contacts);
            closeDialog();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
      <AppDialog
        open={dialog?.type === "deleteChannel"}
        title="刪除聯絡資料"
        message={dialog?.item ? `確定刪除「${channelLabel(dialog.item.type)} ${dialog.item.value}」？` : ""}
        confirmLabel="刪除"
        danger
        onCancel={closeDialog}
        onSubmit={async () => {
          try {
            const channels = await api.deleteContactChannel(dialog.company.id, dialog.contact.id, dialog.item.id);
            replaceChannels(dialog.company.id, dialog.contact.id, channels);
            closeDialog();
          } catch (err) {
            setError(err.message);
            closeDialog();
          }
        }}
      />
    </div>
  );
}
