import { ref, watch } from "vue";

// ── Dark / light ──────────────────────────────────────────────────────────────

const getInitialDark = (): boolean => {
  const stored = localStorage.getItem("theme");
  if (stored) return stored === "dark";
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
};

const isDark = ref(getInitialDark());

function applyDark(dark: boolean) {
  document.documentElement.classList.toggle("dark", dark);
  localStorage.setItem("theme", dark ? "dark" : "light");
}

applyDark(isDark.value);
watch(isDark, applyDark);

// ── Palette ───────────────────────────────────────────────────────────────────

type Palette = "warm" | "slate";

const getInitialPalette = (): Palette =>
  (localStorage.getItem("palette") as Palette) ?? "warm";

const palette = ref<Palette>(getInitialPalette());

function applyPalette(p: Palette) {
  document.documentElement.classList.toggle("palette-slate", p === "slate");
  localStorage.setItem("palette", p);
}

applyPalette(palette.value);
watch(palette, applyPalette);

// ── Public API ────────────────────────────────────────────────────────────────

export function useTheme() {
  const toggleTheme = () => { isDark.value = !isDark.value; };
  const togglePalette = () => { palette.value = palette.value === "warm" ? "slate" : "warm"; };

  return { isDark, toggleTheme, palette, togglePalette };
}
