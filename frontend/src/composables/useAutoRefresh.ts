import { watch } from "vue";
import { useDocumentVisibility, useIntervalFn } from "@vueuse/core";

/**
 * Re-runs `refresh` every `intervalMs` while the tab is visible, pauses while it's hidden,
 * and refreshes immediately when the tab becomes visible again. Overlapping runs are skipped.
 * For pages admins keep open all day, so clock-ins/outs show up without a manual reload.
 */
export function useAutoRefresh(refresh: () => Promise<unknown>, intervalMs = 60_000) {
  let running = false;
  const run = async () => {
    if (running) return;
    running = true;
    try {
      await refresh();
    } finally {
      running = false;
    }
  };

  const { pause, resume } = useIntervalFn(run, intervalMs);
  const visibility = useDocumentVisibility();

  watch(visibility, (state) => {
    if (state === "visible") {
      run();
      resume();
    } else {
      pause();
    }
  });
}
