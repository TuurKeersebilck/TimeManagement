import { ref } from "vue";
import type { ChangelogCategory, ChangelogEntry } from "../components/ChangelogModal.vue";

const CHANGELOG_KEY = "changelog-last-seen";

const entries = ref<ChangelogEntry[]>([]);
const hasUnread = ref(false);
const isFetched = ref(false);

function parseChangelog(md: string): ChangelogEntry[] {
  const result: ChangelogEntry[] = [];
  const sections = md.split(/\n(?=## \[)/);
  for (const section of sections) {
    const vMatch = section.match(/^## \[(v[\d.]+)\] - (\d{4}-\d{2}-\d{2})/);
    if (!vMatch) continue;
    const [, version, date] = vMatch;
    const categories: ChangelogCategory[] = [];
    const catBlocks = section.split(/\n(?=### )/);
    for (const block of catBlocks.slice(1)) {
      const catMatch = block.match(/^### (.+)/);
      if (!catMatch) continue;
      const items = block
        .split("\n")
        .filter((l) => l.startsWith("- "))
        .map((l) => l.slice(2));
      if (items.length) categories.push({ name: catMatch[1], items });
    }
    result.push({ version, date, categories });
  }
  return result;
}

export function useChangelogStore() {
  const fetchChangelog = async (force = false) => {
    if (isFetched.value && !force) return entries.value;
    try {
      const res = await fetch("/CHANGELOG.md");
      const text = await res.text();
      entries.value = parseChangelog(text);
      isFetched.value = true;
      if (entries.value.length) {
        hasUnread.value = localStorage.getItem(CHANGELOG_KEY) !== entries.value[0].version;
      }
    } catch {
      /* non-critical, silently ignore */
    }
    return entries.value;
  };

  const markSeen = () => {
    if (entries.value.length) {
      localStorage.setItem(CHANGELOG_KEY, entries.value[0].version);
      hasUnread.value = false;
    }
  };

  return { entries, hasUnread, fetchChangelog, markSeen };
}
