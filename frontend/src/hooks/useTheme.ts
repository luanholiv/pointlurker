import { useEffect, useState } from "react";
import { withViewTransition } from "../lib/viewTransition";

type Theme = "light" | "dark";

/** The viewport point the new theme spreads out from, usually the toggle's centre. */
export interface RevealOrigin {
  x: number;
  y: number;
}

export interface UseThemeReturn {
  theme: Theme;
  toggle: (origin?: RevealOrigin) => void;
}

function getInitialTheme(): Theme {
  const stored = localStorage.getItem("pl.theme");
  if (stored === "light" || stored === "dark") return stored;
  return "dark";
}

function setRevealOrigin({ x, y }: RevealOrigin) {
  const root = document.documentElement;
  const radius = Math.hypot(
    Math.max(x, window.innerWidth - x),
    Math.max(y, window.innerHeight - y),
  );
  root.style.setProperty("--reveal-x", `${x}px`);
  root.style.setProperty("--reveal-y", `${y}px`);
  root.style.setProperty("--reveal-r", `${radius}px`);
  root.setAttribute("data-theme-switching", "");
}

export function useTheme(): UseThemeReturn {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("pl.theme", theme);
  }, [theme]);

  const toggle = (origin?: RevealOrigin) => {
    const next: Theme = theme === "light" ? "dark" : "light";
    // The attribute is set here as well as in the effect: the transition snapshots the DOM as
    // soon as this callback returns, before passive effects are guaranteed to have run.
    const apply = () => {
      document.documentElement.setAttribute("data-theme", next);
      setTheme(next);
    };
    if (!origin) {
      apply();
      return;
    }
    void withViewTransition(apply, () => setRevealOrigin(origin)).finally(() =>
      document.documentElement.removeAttribute("data-theme-switching"),
    );
  };

  return { theme, toggle };
}
