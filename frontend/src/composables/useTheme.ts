import { ref, watch } from "vue";

const getInitialTheme = (): boolean => {
	const stored = localStorage.getItem("theme");
	if (stored) return stored === "dark";
	return window.matchMedia("(prefers-color-scheme: dark)").matches;
};

const isDark = ref(getInitialTheme());

function applyTheme(dark: boolean) {
	if (dark) {
		document.documentElement.classList.add("dark");
	} else {
		document.documentElement.classList.remove("dark");
	}
	localStorage.setItem("theme", dark ? "dark" : "light");
}

// Apply immediately on module load
applyTheme(isDark.value);

watch(isDark, applyTheme);

export function useTheme() {
	const toggleTheme = () => {
		isDark.value = !isDark.value;
	};

	return { isDark, toggleTheme };
}
