import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Heart } from "lucide-react";
import Pill from "../../components/ui/Pill";
import Chip from "../../components/ui/Chip";
import { formatNumber } from "../../i18n/formatters";
import type { StoreItemDetail } from "./storesApi";

const CAT_GLYPH: Record<string, string> = {
  AUDIO: "♪",
  ASSINATURA: "★",
  PIX: "₿",
  SKIN: "◆",
  GIFT: "❖",
  SUB: "◉",
};

function itemHue(name: string, cat: string): number {
  const seed = name + cat;
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  return h;
}

interface ItemCardProps {
  item: StoreItemDetail;
  onWishlist: () => void;
  wishlisted?: boolean;
}

const ItemCard = ({ item, onWishlist, wishlisted = false }: ItemCardProps) => {
  const { t, i18n } = useTranslation();
  const h = itemHue(item.name, item.categoryName);
  const bg = `linear-gradient(135deg, oklch(0.42 0.12 ${h}), oklch(0.24 0.09 ${(h + 50) % 360}))`;
  const catKey = item.categoryName.toUpperCase();
  const glyph = CAT_GLYPH[catKey] ?? "◇";
  const [thumbnailFailed, setThumbnailFailed] = useState(false);
  const showThumbnail = Boolean(item.thumbnailUrl) && !thumbnailFailed;

  // The burst marks the moment an item joins the wishlist, so it plays on the false-to-true
  // change only, never on a card that mounts already wishlisted.
  const heartRef = useRef<HTMLButtonElement>(null);
  const wasWishlisted = useRef(wishlisted);
  useEffect(() => {
    const button = heartRef.current;
    if (button && wishlisted && !wasWishlisted.current) {
      button.classList.remove("pop");
      void button.offsetWidth;
      button.classList.add("pop");
    }
    wasWishlisted.current = wishlisted;
  }, [wishlisted]);

  return (
    <div className="card item-card">
      <div className="item-media" style={{ background: bg }}>
        {showThumbnail ? (
          <img
            src={item.thumbnailUrl ?? undefined}
            alt=""
            referrerPolicy="no-referrer"
            onError={() => setThumbnailFailed(true)}
            style={{
              position: "absolute",
              inset: 0,
              zIndex: 1,
              width: "100%",
              height: "100%",
              objectFit: "cover",
            }}
          />
        ) : (
          <>
            <span className="item-media-glyph">{glyph}</span>
            <span className="item-media-cat">
              {item.categoryName.toUpperCase()}
            </span>
          </>
        )}
      </div>

      <div className="vstack" style={{ gap: 8 }}>
        <div className="between" style={{ alignItems: "flex-start" }}>
          <div style={{ maxWidth: "75%" }}>
            <div className="title t-subheading">{item.name}</div>
            {item.description && (
              <div className="desc t-small t-muted" style={{ marginTop: 3 }}>
                {item.description}
              </div>
            )}
          </div>
          <Pill tone={item.isAvailable ? "ok" : "bad"}>
            {item.isAvailable ? t("item.available") : t("item.unavailable")}
          </Pill>
        </div>

        <div
          className="price t-mono"
          style={{
            color: "var(--ink)",
            letterSpacing: "0.02em",
          }}
        >
          {formatNumber(item.cost, i18n.language)}
          <span className="t-meta t-muted" style={{ marginLeft: 2 }}>
            {" "}
            {t("common.pts")}
          </span>
        </div>

        {item.quantityCurrent !== -1 && (
          <p className="t-small t-muted">
            {t("item.stock", {
              current: item.quantityCurrent,
              total: item.quantityTotal,
            })}
          </p>
        )}

        <div
          className="tags"
          style={{ display: "flex", gap: 6, flexWrap: "wrap" }}
        >
          <Chip>{item.categoryName}</Chip>
          {item.subscriberOnly && <Chip>{t("item.subOnly")}</Chip>}
        </div>

        <div
          className="footer"
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            paddingTop: 6,
            borderTop: "1px solid var(--line-soft)",
          }}
        >
          <span className="t-meta t-dim">
            {item.providerItemId?.slice(0, 8).toUpperCase()}
          </span>
          <button
            ref={heartRef}
            className={`btn btn-sm heart-btn${wishlisted ? " btn-accent" : ""}`}
            onClick={onWishlist}
            onAnimationEnd={(e) => e.currentTarget.classList.remove("pop")}
          >
            <Heart size={13} aria-hidden={true} focusable="false" />
            {wishlisted ? t("wishlist.wishlisted") : t("wishlist.add")}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ItemCard;
