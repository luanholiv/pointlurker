interface SkeletonGridProps {
  label: string;
  count?: number;
  media?: boolean;
}

/**
 * Placeholder cards in the same grid the data will fill, so nothing shifts when it arrives.
 * The label stays in the DOM for assistive tech; the bars are decoration.
 */
const SkeletonGrid = ({
  label,
  count = 6,
  media = true,
}: SkeletonGridProps) => (
  <div className="grid grid-3" role="status" aria-busy={true}>
    <span className="sr-only">{label}</span>
    {Array.from({ length: count }, (_, i) => (
      <div className="skeleton-card" key={i} aria-hidden={true}>
        {media && <div className="skeleton-bar skeleton-media" />}
        <div className="skeleton-bar skeleton-line mid" />
        <div className="skeleton-bar skeleton-line short" />
        <div className="skeleton-bar skeleton-line" />
      </div>
    ))}
  </div>
);

export default SkeletonGrid;
