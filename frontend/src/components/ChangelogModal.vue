<script setup lang="ts">
import {
  Dialog,
  DialogScrollContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

export interface ChangelogCategory {
  name: string;
  items: string[];
}

export interface ChangelogEntry {
  version: string;
  date: string;
  categories: ChangelogCategory[];
}

defineProps<{
  entries: ChangelogEntry[];
}>();

const open = defineModel<boolean>("open", { default: false });
</script>

<template>
  <Dialog v-model:open="open">
    <DialogScrollContent class="max-w-xl">
      <DialogHeader>
        <DialogTitle class="text-base">What's new</DialogTitle>
      </DialogHeader>

      <div v-if="entries.length === 0" class="py-8 text-center text-sm text-muted-foreground">
        Loading...
      </div>

      <div v-else class="space-y-8 mt-2">
        <div v-for="entry in entries" :key="entry.version">
          <!-- Version header -->
          <div class="flex items-baseline gap-3 mb-4">
            <span class="text-sm font-semibold text-foreground">{{ entry.version }}</span>
            <span class="text-xs text-muted-foreground">{{ entry.date }}</span>
          </div>

          <!-- Categories -->
          <div
            v-for="cat in entry.categories"
            :key="cat.name"
            class="mb-4"
          >
            <p class="text-[11px] uppercase tracking-widest text-muted-foreground mb-2">
              {{ cat.name }}
            </p>
            <ul class="space-y-1.5">
              <li
                v-for="(item, i) in cat.items"
                :key="i"
                class="flex gap-2 text-sm text-foreground/80"
              >
                <span class="mt-1.5 size-1 shrink-0 rounded-full bg-muted-foreground/50" />
                {{ item }}
              </li>
            </ul>
          </div>

          <div v-if="entries.indexOf(entry) < entries.length - 1" class="border-t border-border" />
        </div>
      </div>
    </DialogScrollContent>
  </Dialog>
</template>
