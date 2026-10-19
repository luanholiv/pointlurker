interface NotificationDotProps {
  label: string;
}

const NotificationDot = ({ label }: NotificationDotProps) => (
  <span className="notification-dot" data-testid="notification-dot">
    <span className="sr-only">{label}</span>
  </span>
);

export default NotificationDot;
