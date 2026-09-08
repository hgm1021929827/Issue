export default function PageTitle({ icon: Icon, children, as: Tag = "h1" }) {
  return (
    <Tag className="page-title">
      {Icon ? <Icon className="page-title-icon" size={22} strokeWidth={1.75} /> : null}
      <span>{children}</span>
    </Tag>
  );
}
