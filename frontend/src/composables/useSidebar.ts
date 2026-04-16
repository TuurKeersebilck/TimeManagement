import { ref } from "vue";

// Module-level state — persists across route navigations
const sidebarOpen = ref(true);

// Reopen the sidebar when resizing back to desktop so it doesn't stay
// hidden after being closed at a mobile breakpoint.
if (typeof window !== "undefined") {
  window.matchMedia("(min-width: 1024px)").addEventListener("change", (e) => {
    if (e.matches) sidebarOpen.value = true;
  });
}

export function useSidebar() {
  const toggle = () => {
    sidebarOpen.value = !sidebarOpen.value;
  };

  return { sidebarOpen, toggle };
}
