<script setup lang="ts">
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { XIcon } from "lucide-vue-next";
import { emptySession, type EditableSession } from "@/lib/daySessions";

/**
 * Edits one day's sessions and their breaks. Used by the employee's adjustment request and the
 * admin's direct edit, so both enter times the same way.
 */
const props = withDefaults(
  defineProps<{
    /** Allow removing the last session (admin: an empty day removes its hours). */
    allowEmpty?: boolean;
  }>(),
  { allowEmpty: false }
);

const sessions = defineModel<EditableSession[]>({ required: true });

function addSession() {
  sessions.value.push(emptySession());
}

function removeSession(idx: number) {
  sessions.value.splice(idx, 1);
  if (sessions.value.length === 0 && !props.allowEmpty) sessions.value.push(emptySession());
}

function addBreak(sessionIdx: number) {
  sessions.value[sessionIdx].breaks.push({ breakStart: "", breakEnd: "" });
}

function removeBreak(sessionIdx: number, breakIdx: number) {
  sessions.value[sessionIdx].breaks.splice(breakIdx, 1);
}
</script>

<template>
  <div class="space-y-2">
    <div
      v-for="(session, si) in sessions"
      :key="si"
      class="rounded-lg border border-slate-200 dark:border-slate-700 p-3 space-y-2.5"
    >
      <div class="flex items-center justify-between">
        <span class="text-xs font-medium text-slate-600 dark:text-slate-400">
          Session {{ si + 1 }}
          <span v-if="session.note" class="text-slate-400">({{ session.note }})</span>
        </span>
        <Button
          v-if="sessions.length > 1 || allowEmpty"
          size="icon"
          variant="ghost"
          class="size-6"
          title="Remove session"
          @click="removeSession(si)"
        >
          <XIcon class="size-3" />
        </Button>
      </div>

      <div class="grid grid-cols-2 gap-2">
        <div class="space-y-1">
          <Label class="text-xs text-slate-500">Clock In <span class="text-destructive">*</span></Label>
          <Input v-model="session.clockIn" type="time" />
        </div>
        <div class="space-y-1">
          <Label class="text-xs text-slate-500">Clock Out <span class="text-destructive">*</span></Label>
          <Input v-model="session.clockOut" type="time" />
        </div>
      </div>

      <!-- Breaks -->
      <div
        v-if="session.breaks.length"
        class="ml-2 pl-2.5 border-l-2 border-slate-100 dark:border-slate-800 space-y-2"
      >
        <div v-for="(b, bi) in session.breaks" :key="bi" class="grid grid-cols-2 gap-2 items-end">
          <div class="space-y-1">
            <Label class="text-xs text-slate-400">Break Start</Label>
            <Input v-model="b.breakStart" type="time" class="h-8 text-xs" />
          </div>
          <div class="flex gap-1 items-end">
            <div class="flex-1 space-y-1">
              <Label class="text-xs text-slate-400">Break End</Label>
              <Input v-model="b.breakEnd" type="time" class="h-8 text-xs" />
            </div>
            <Button size="icon" variant="ghost" class="size-8 shrink-0" title="Remove break" @click="removeBreak(si, bi)">
              <XIcon class="size-3" />
            </Button>
          </div>
        </div>
      </div>

      <Button variant="ghost" size="sm" class="h-7 text-xs text-slate-500 px-2" @click="addBreak(si)">
        + Add break
      </Button>
    </div>

    <Button variant="ghost" size="sm" class="h-7 text-xs text-slate-500 px-2" @click="addSession">
      + Add session
    </Button>
  </div>
</template>
