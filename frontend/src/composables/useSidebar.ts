import { ref } from "vue";

// Module-level state — persists across route navigations
const sidebarOpen = ref(true);

export function useSidebar() {
  const toggle = () => {
    sidebarOpen.value = !sidebarOpen.value;
  };

  return { sidebarOpen, toggle };
}
