import { useLayoutEffect, useRef } from "react";

export default function AppTextarea({ value = "", onChange, rows = 2, ...props }) {
  const ref = useRef(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = "";
    if (el.scrollHeight > el.clientHeight) {
      el.style.height = `${el.scrollHeight}px`;
    }
  }, [value]);

  return <textarea ref={ref} rows={rows} value={value} onChange={onChange} {...props} />;
}
