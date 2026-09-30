<script setup lang="ts">
import { ref } from "vue";
import { useAuth } from "@/composables/useAuth";
import {
  ClockIcon,
  UmbrellaIcon,
  CalendarIcon,
  BellIcon,
  MailIcon,
  ChevronDownIcon,
  LayoutDashboardIcon,
  PencilLineIcon,
  ScaleIcon,
  FileSpreadsheetIcon,
  UsersIcon,
  PlaneIcon,
  SettingsIcon,
} from "lucide-vue-next";

const { isAdmin } = useAuth();

const open = ref<Record<string, boolean>>({
  time: true,
  vacations: false,
  calendar: false,
  notifications: false,
  emails: false,
  adminDashboard: false,
  adminTimeLogs: false,
  adminSettlements: false,
  adminExport: false,
  adminEmployees: false,
  adminLeave: false,
  adminSettings: false,
});

function toggle(section: string) {
  open.value[section] = !open.value[section];
}
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-2xl mx-auto">

      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Help</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">How LOGR works — features, emails, and calendar sync explained</p>
      </div>

      <div class="space-y-3">

        <!-- ── Time tracking ──────────────────────────────────────────── -->
        <div class="card overflow-hidden">
          <button
            class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
            @click="toggle('time')"
          >
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-950 flex items-center justify-center shrink-0">
                <ClockIcon class="size-4 text-indigo-600 dark:text-indigo-400" />
              </div>
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Time tracking</span>
            </div>
            <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.time && 'rotate-180']" />
          </button>

          <div v-show="open.time" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Clock sequence</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Each session follows the order: <span class="font-medium text-slate-700 dark:text-slate-300">Clock in → Break start → Break end → Clock out</span>. You can't skip steps or go out of order. A day can contain multiple sessions — for example if you leave and return later in the day.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">5-minute window</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Each clock event must be submitted within <span class="font-medium text-slate-700 dark:text-slate-300">5 minutes</span> of when it actually happened. If you miss this window, submit a <span class="font-medium text-slate-700 dark:text-slate-300">time adjustment request</span> — existing sessions for that day are pre-filled so you only need to correct what's wrong. An admin will review and apply the correction.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Break minimum</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">If a minimum break duration is configured for your account, the End Break button stays disabled until that minimum is met. A live countdown in M:SS ticks down while you're on break; once it reaches zero the button unlocks and switches to showing the elapsed time.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Flex balance</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Every completed day is compared against your per-weekday target hours. The time-tracking view shows a <span class="font-medium text-slate-700 dark:text-slate-300">daily delta</span> (how much over or under target you were) and a <span class="font-medium text-slate-700 dark:text-slate-300">running monthly flex balance</span>. Days with an open session are excluded from the balance until you clock out.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Work from home</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Toggle the WFH flag at any point during the day to mark it as remote. You can also change it after the fact from the time-tracking view.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Vacation interaction</p>
              <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                <li><span class="font-medium text-slate-700 dark:text-slate-300">Full-day vacation</span> — clock-in is blocked for that day</li>
                <li><span class="font-medium text-slate-700 dark:text-slate-300">Half-day vacation (0.5)</span> — only clock in and clock out are allowed; no break events</li>
              </ul>
            </div>
          </div>
        </div>

        <!-- ── Vacations ──────────────────────────────────────────────── -->
        <div class="card overflow-hidden">
          <button
            class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
            @click="toggle('vacations')"
          >
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-emerald-100 dark:bg-emerald-950 flex items-center justify-center shrink-0">
                <UmbrellaIcon class="size-4 text-emerald-600 dark:text-emerald-400" />
              </div>
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Vacations</span>
            </div>
            <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.vacations && 'rotate-180']" />
          </button>

          <div v-show="open.vacations" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Requesting leave</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Select a single day or a date range, choose a vacation type (e.g. Annual, Sick), and optionally add a note. Half-day requests use an amount of <span class="font-medium text-slate-700 dark:text-slate-300">0.5</span>.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Balance</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Your yearly allowance, days used, and days remaining are shown per vacation type at the top of the Vacations page. Balances are set by your admin.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Deleting requests</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Upcoming vacation days can be deleted before the date arrives. Past days cannot be removed.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Team calendar</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">The Team Calendar shows everyone's scheduled time off, color-coded by vacation type. It's read-only — use it to see who's available.</p>
            </div>
          </div>
        </div>

        <!-- ── Calendar feed ──────────────────────────────────────────── -->
        <div class="card overflow-hidden">
          <button
            class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
            @click="toggle('calendar')"
          >
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-sky-100 dark:bg-sky-950 flex items-center justify-center shrink-0">
                <CalendarIcon class="size-4 text-sky-600 dark:text-sky-400" />
              </div>
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Calendar sync</span>
            </div>
            <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.calendar && 'rotate-180']" />
          </button>

          <div v-show="open.calendar" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">What it is</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Your vacation days are available as a personal <span class="font-medium text-slate-700 dark:text-slate-300">iCalendar (.ics) feed</span> you can subscribe to in Google Calendar, Apple Calendar, Outlook, or any app that supports iCal subscriptions.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">How to set up</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Go to <span class="font-medium text-slate-700 dark:text-slate-300">Account → Calendar subscription</span>, generate a feed URL, and subscribe via the link or by pasting the URL into your calendar app.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">One-way sync</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">The feed is <span class="font-medium text-slate-700 dark:text-slate-300">read-only</span>. Events appear in your calendar app but you cannot create, edit, or delete them from there. All changes are made in LOGR.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Sync delay</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">Your calendar app polls the feed on its own schedule — changes may take <span class="font-medium text-slate-700 dark:text-slate-300">up to 24 hours</span> to appear. If you need it sooner, trigger a manual sync in your calendar app.</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">iOS note: the default refresh interval for subscribed calendars is <span class="font-medium text-slate-700 dark:text-slate-300">once a week</span>. Change it under Settings → Calendar → Accounts → Subscribed Calendars.</p>
            </div>

            <div class="space-y-1.5">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Token expiry & regeneration</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">The feed URL is valid for one year. When it expires — or if you choose to regenerate early — the old URL stops working <span class="font-medium text-slate-700 dark:text-slate-300">immediately</span>. Any calendar app still subscribed to the old URL will fail to sync and may show an error or stop updating.</p>
              <p class="text-sm text-slate-600 dark:text-slate-400">After regenerating, copy the new URL from <span class="font-medium text-slate-700 dark:text-slate-300">Account → Calendar subscription</span> and re-subscribe in every calendar app you use. The URL is only shown in the browser where you generated it, so copy it before switching devices.</p>
            </div>
          </div>
        </div>

        <!-- ── Notifications ──────────────────────────────────────────── -->
        <div class="card overflow-hidden">
          <button
            class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
            @click="toggle('notifications')"
          >
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-amber-100 dark:bg-amber-950 flex items-center justify-center shrink-0">
                <BellIcon class="size-4 text-amber-600 dark:text-amber-400" />
              </div>
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100">In-app notifications</span>
            </div>
            <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.notifications && 'rotate-180']" />
          </button>

          <div v-show="open.notifications" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
            <p class="text-sm text-slate-600 dark:text-slate-400">The bell icon in the top-right of the sidebar shows unread notifications. You receive one when:</p>
            <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
              <li>An admin <span class="font-medium text-slate-700 dark:text-slate-300">approves</span> your time adjustment request</li>
              <li>An admin <span class="font-medium text-slate-700 dark:text-slate-300">rejects</span> your time adjustment request</li>
            </ul>
            <p class="text-sm text-slate-600 dark:text-slate-400">Notifications are not pushed in real time — the app polls periodically. Mark them read individually or all at once via the bell dropdown.</p>
          </div>
        </div>

        <!-- ── Emails ─────────────────────────────────────────────────── -->
        <div class="card overflow-hidden">
          <button
            class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
            @click="toggle('emails')"
          >
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-violet-100 dark:bg-violet-950 flex items-center justify-center shrink-0">
                <MailIcon class="size-4 text-violet-600 dark:text-violet-400" />
              </div>
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Emails you'll receive</span>
            </div>
            <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.emails && 'rotate-180']" />
          </button>

          <div v-show="open.emails" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
            <div class="space-y-3">
              <div class="space-y-0.5">
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Password reset</p>
                <p class="text-sm text-slate-500 dark:text-slate-400">Sent when you request a reset via the login page. The link expires in <span class="font-medium text-slate-600 dark:text-slate-300">1 hour</span> and can only be used once.</p>
              </div>

              <div class="border-t border-border" />

              <div class="space-y-0.5">
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Time adjustment outcome</p>
                <p class="text-sm text-slate-500 dark:text-slate-400">Sent when an admin approves or rejects your adjustment request, confirming whether your time log was updated.</p>
              </div>

              <div class="border-t border-border" />

              <div class="space-y-0.5">
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Missed clock-in reminder</p>
                <p class="text-sm text-slate-500 dark:text-slate-400">Sent at <span class="font-medium text-slate-600 dark:text-slate-300">08:00 UTC</span> on working days if no clock-in was recorded the previous day. You can safely ignore it if you were on approved leave. Submit a time adjustment request if you need to log that day.</p>
              </div>
            </div>
          </div>
        </div>

        <!-- ══ For admins ═══════════════════════════════════════════════ -->
        <template v-if="isAdmin">
          <p class="pt-4 text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
            For admins
          </p>

          <!-- ── Dashboard ──────────────────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminDashboard')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-amber-100 dark:bg-amber-950 flex items-center justify-center shrink-0">
                  <LayoutDashboardIcon class="size-4 text-amber-600 dark:text-amber-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Dashboard — today at a glance</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminDashboard && 'rotate-180']" />
            </button>
            <div v-show="open.adminDashboard" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">One row per employee</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Start, break start and end, end, hours worked today (with a bar toward today's target) and this month's <span class="font-medium text-slate-700 dark:text-slate-300">flex balance</span>. Click a row to open that employee's time logs.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Status</p>
                <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Working</span> / <span class="font-medium text-slate-700 dark:text-slate-300">On break</span> — clocked in right now; hours count up live</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Clocked out</span> — done for the day</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Not clocked in</span> (red) — a working day with nothing logged yet</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">On leave</span> / <span class="font-medium text-slate-700 dark:text-slate-300">Day off</span> — full-day leave, weekend, holiday or a non-working weekday for that person</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Auto-closed</span> — a session was left open too long and closed automatically; fix it under All Time Logs</li>
                </ul>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Always current</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">The page refreshes itself every minute while it's visible (see "Live · updated" in the top right) and moves on to the new day after midnight.</p>
              </div>
            </div>
          </div>

          <!-- ── Time logs & corrections ────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminTimeLogs')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-950 flex items-center justify-center shrink-0">
                  <PencilLineIcon class="size-4 text-indigo-600 dark:text-indigo-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Time logs & corrections</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminTimeLogs && 'rotate-180']" />
            </button>
            <div v-show="open.adminTimeLogs" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">All Time Logs</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Every logged day, filtered by employee and period (this month by default). The cards on top — <span class="font-medium text-slate-700 dark:text-slate-300">flex balance</span>, <span class="font-medium text-slate-700 dark:text-slate-300">hours worked</span> and <span class="font-medium text-slate-700 dark:text-slate-300">WFH days</span> — follow the same filters. The flex balance there is what was built up in that period (worked minus target, plus manual flex adjustments), without carry-overs from earlier settlements.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Edit, add or delete hours yourself</p>
                <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Edit</span> — the pencil at the end of a row opens that day's sessions and breaks. Changes apply immediately.</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Add hours</span> — select an employee, click Add hours and pick a date, e.g. when someone forgot to clock in.</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Delete day</span> — removes all of that day's hours after a confirmation.</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Auto-closed sessions</span> — a forgotten clock-out shows as "auto-closed"; fill in the clock-out time and the day counts again.</li>
                </ul>
                <p class="text-sm text-slate-600 dark:text-slate-400">A reason is optional. The employee isn't notified, but every change is kept in the adjustment history marked <span class="font-medium text-slate-700 dark:text-slate-300">Admin edit</span> (who, when and why). A day can't be edited while the employee is still clocked in on it. In an already settled month you'll see a warning: the settlement keeps its confirmed numbers, so correct the balance with a flex adjustment if needed.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Adjustment requests from employees</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Found under <span class="font-medium text-slate-700 dark:text-slate-300">Settings → Adjustment Requests</span>. A badge in the menu shows how many are waiting. Approve or reject them there, or approve with the one-click link in the notification email. Approving replaces that day's sessions with the requested ones. Pending requests block the month's settlement until they're handled.</p>
              </div>
            </div>
          </div>

          <!-- ── Flex balance & settlements ─────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminSettlements')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-emerald-100 dark:bg-emerald-950 flex items-center justify-center shrink-0">
                  <ScaleIcon class="size-4 text-emerald-600 dark:text-emerald-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Flex balance & monthly settlements</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminSettlements && 'rotate-180']" />
            </button>
            <div v-show="open.adminSettlements" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">How the balance is calculated</p>
                <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                  <li>Each day, hours worked are compared with that weekday's target (half a target on a half day of leave, none on full leave, holidays or weekends).</li>
                  <li>If no break was logged, the minimum break is deducted automatically.</li>
                  <li>Days with an open session don't count until the employee clocks out.</li>
                  <li>An employee counts from their first logged day, and a disabled employee until their last — so a mid-month start doesn't show up as missed days.</li>
                  <li>Flex adjustments (manual or carried over from a settlement) are added to the month they're dated in.</li>
                </ul>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Overtime is what's left in the flex balance</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Hours worked beyond the target first make up for days that were shorter than the target — two hours longer on Monday and two hours shorter on Tuesday leave <span class="font-medium text-slate-700 dark:text-slate-300">0</span>. Only what's left over in the flex balance at the end of the month counts as overtime and can be paid out; that's decided in the settlement.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Settlements</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">On the 1st of each month a settlement is created automatically for every active employee for the previous month; you can also <span class="font-medium text-slate-700 dark:text-slate-300">Generate</span> them yourself for any completed month. To confirm one, split the month's balance into <span class="font-medium text-slate-700 dark:text-slate-300">pay out</span>, <span class="font-medium text-slate-700 dark:text-slate-300">carry over</span> to next month and/or <span class="font-medium text-slate-700 dark:text-slate-300">deduct from next month</span>, and add notes if you like. Carry-overs appear as flex adjustments in the next month. Open or auto-closed sessions and pending adjustment requests in that month must be resolved first. Confirmed settlements are locked.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Reminders by email</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">When settlements are created on the 1st, the admin notification address gets an email that they're ready, followed by a reminder every Monday while any are still unconfirmed. Turn this off under App Settings → Email types.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Manual flex adjustments</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">On an employee's page, add or deduct hours from their flex balance with a date and reason (e.g. a training evening that wasn't clocked). Not possible in a month that's already settled; carry-over adjustments created by a settlement can't be deleted.</p>
              </div>
            </div>
          </div>

          <!-- ── Payroll export ─────────────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminExport')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-sky-100 dark:bg-sky-950 flex items-center justify-center shrink-0">
                  <FileSpreadsheetIcon class="size-4 text-sky-600 dark:text-sky-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Payroll export</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminExport && 'rotate-180']" />
            </button>
            <div v-show="open.adminExport" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">What's in the export</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Made for entering hours into payroll, from the Payroll Export page or the Settlements page. Pick a month and one employee or everyone. One row per employee per day with:</p>
                <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Hours worked</span> and <span class="font-medium text-slate-700 dark:text-slate-300">overtime</span> that day as exact decimal hours with two decimals — 30 min = 0.5, 1h30 = 1.5, 40 min = 0.67. Overtime is the difference with that day's target, so a day that's shorter than the target is negative (e.g. −2 for 6 hours on an 8-hour day).</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Leave type</span> and <span class="font-medium text-slate-700 dark:text-slate-300">leave days</span> (1 or 0.5), a public holiday, or "Missing Log" for a working day with nothing recorded</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">WFH</span> (yes or no)</li>
                </ul>
                <p class="text-sm text-slate-600 dark:text-slate-400">Month totals follow below the table: <span class="font-medium text-slate-700 dark:text-slate-300">total overtime</span> (the days added up, so +2 on Monday and −2 on Tuesday make 0), <span class="font-medium text-slate-700 dark:text-slate-300">adjustments</span> (a carry-over from last month or a manual flex adjustment), the <span class="font-medium text-slate-700 dark:text-slate-300">flex balance</span> (the two together — the same number as on the dashboard) and the <span class="font-medium text-slate-700 dark:text-slate-300">approved overtime</span> from the settlement — the hours to pay out. The file uses <span class="font-medium text-slate-700 dark:text-slate-300">;</span> as separator and the same numbers as the flex balance (including the automatic minimum break).</p>
              </div>
            </div>
          </div>

          <!-- ── Employees ──────────────────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminEmployees')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-rose-100 dark:bg-rose-950 flex items-center justify-center shrink-0">
                  <UsersIcon class="size-4 text-rose-600 dark:text-rose-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Employees</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminEmployees && 'rotate-180']" />
            </button>
            <div v-show="open.adminEmployees" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Inviting and removing</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Found under <span class="font-medium text-slate-700 dark:text-slate-300">Settings → Employees</span>. Invite someone by email; the link is valid for <span class="font-medium text-slate-700 dark:text-slate-300">48 hours</span>. When someone leaves, <span class="font-medium text-slate-700 dark:text-slate-300">disable</span> their account: they can no longer log in, and days after their last logged day no longer count in their balance. Their history stays available (they're marked "disabled" in filters). An account must be disabled before it can be deleted permanently, which also removes all of its data.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Per-employee settings</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Click an employee to set their vacation balances, their own weekly working-hours target (per weekday, e.g. 0h on Wednesdays for a 4/5 schedule) and minimum break — each falls back to the global default when left empty. The page also shows their weekly hours, planned leave, settlement history and flex adjustments.</p>
              </div>
            </div>
          </div>

          <!-- ── Leave & holidays ───────────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminLeave')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-violet-100 dark:bg-violet-950 flex items-center justify-center shrink-0">
                  <PlaneIcon class="size-4 text-violet-600 dark:text-violet-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Leave & holidays</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminLeave && 'rotate-180']" />
            </button>
            <div v-show="open.adminLeave" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Vacation types</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Under <span class="font-medium text-slate-700 dark:text-slate-300">Settings → Vacation Types</span>: create types (name, color, description), then give employees a yearly balance per type on their employee page.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Planning leave for someone</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">On <span class="font-medium text-slate-700 dark:text-slate-300">My Vacations</span>, choose an employee at the top to plan or remove leave on their behalf; they get a notification. The Team Calendar shows everyone's leave.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Public holidays</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Under <span class="font-medium text-slate-700 dark:text-slate-300">Settings → App Settings</span>, choose the country to import its public holidays. Mark a holiday as a working day if the company works on it, or add your own company holidays.</p>
              </div>
            </div>
          </div>

          <!-- ── Settings & emails ──────────────────────────────────────── -->
          <div class="card overflow-hidden">
            <button class="w-full flex items-center justify-between p-5 text-left cursor-pointer" @click="toggle('adminSettings')">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center shrink-0">
                  <SettingsIcon class="size-4 text-slate-600 dark:text-slate-300" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Settings & emails</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.adminSettings && 'rotate-180']" />
            </button>
            <div v-show="open.adminSettings" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">App Settings</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">Global defaults: working-hours target per weekday, minimum break, country and public holidays. Per-employee settings override the defaults.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Emails the app sends</p>
                <ul class="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Adjustment request</span> — to the admin notification address when an employee submits one, with a one-click approve link</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Settlement reminders</span> — to the admin notification address when settlements are ready, and every Monday while any are pending</li>
                  <li><span class="font-medium text-slate-700 dark:text-slate-300">Missed clock-in</span> — to an employee the morning after a working day without any clock-in (not on leave)</li>
                </ul>
                <p class="text-sm text-slate-600 dark:text-slate-400">Each can be switched off under App Settings → Email types; leaving the notification address empty stops the admin emails.</p>
              </div>
              <div class="space-y-1.5">
                <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">Automatic clean-up</p>
                <p class="text-sm text-slate-600 dark:text-slate-400">A session left open for more than <span class="font-medium text-slate-700 dark:text-slate-300">13 hours</span> is closed automatically and marked auto-closed; the employee is notified and can send an adjustment request, or you can fix it yourself under All Time Logs.</p>
              </div>
            </div>
          </div>
        </template>

      </div>
    </div>
  </div>
</template>
