import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";
import AppSelect from "./AppSelect.jsx";

export default function VendorSelect({ value, onChange, disabled = false }) {
  const [companies, setCompanies] = useState([]);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState("");

  const load = async () => {
    setCompanies(await api.clientCompanies());
  };

  useEffect(() => {
    load().catch((err) => setError(err.message));
  }, []);

  return (
    <div className="vendor-field">
      <AppSelect
        value={value}
        disabled={disabled}
        placeholder="請選擇客戶公司"
        options={[
          { value: "", label: "請選擇客戶公司" },
          ...companies.map((c) => ({ value: c.id, label: c.name }))
        ]}
        onChange={onChange}
      />
      {!disabled && (
      <button type="button" className="btn btn-add" onClick={() => { setError(""); setOpen(true); }}>
        新增廠商
      </button>
      )}
      {createPortal(
        <AppDialog
          open={open}
          title="新增客戶公司"
          confirmLabel="新增並選取"
          fields={[{ name: "name", label: "客戶公司名稱", required: true }]}
          initial={{ name: "" }}
          onCancel={() => setOpen(false)}
          onSubmit={async (values) => {
            try {
              const created = await api.createClientCompany({ name: values.name });
              await load();
              onChange(created.id);
              setOpen(false);
              setError("");
            } catch (err) {
              setError(err.message);
            }
          }}
        >
          {error && <p className="banner error">{error}</p>}
        </AppDialog>,
        document.body
      )}
    </div>
  );
}
