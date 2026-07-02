<script setup lang="ts">
import { ref } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useAuth } from "@/composables/useAuth";
import {
  ClockIcon,
  UmbrellaIcon,
  CalendarIcon,
  BellIcon,
  MailIcon,
  ShieldIcon,
  ChevronDownIcon,
} from "lucide-vue-next";

const { isAdmin } = useAuth();

const open = ref<Record<string, boolean>>({
  time: true,
  vacations: false,
  calendar: false,
  notifications: false,
  emails: false,
  admin: false,
});

function toggle(section: string) {
  open.value[section] = !open.value[section];
}
</script>

<template>
  <AuthenticatedLayout>
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

          <!-- ── Admin ──────────────────────────────────────────────────── -->
          <div v-if="isAdmin" class="card overflow-hidden">
            <button
              class="w-full flex items-center justify-between p-5 text-left cursor-pointer"
              @click="toggle('admin')"
            >
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-rose-100 dark:bg-rose-950 flex items-center justify-center shrink-0">
                  <ShieldIcon class="size-4 text-rose-600 dark:text-rose-400" />
                </div>
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">Admin features</span>
              </div>
              <ChevronDownIcon :class="['size-4 text-slate-400 transition-transform duration-200', open.admin && 'rotate-180']" />
            </button>

            <div v-show="open.admin" class="px-5 pb-5 space-y-4 border-t border-border pt-4">
              <div class="space-y-3">
                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Employees</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">Invite new employees by email (invite link expires in <span class="font-medium text-slate-600 dark:text-slate-300">48 hours</span>). Enable or disable accounts — disabled users can't log in but existing sessions stay valid until they expire. An account must be disabled before it can be deleted.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Time logs & adjustment requests</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">View all work sessions across the team, with per-day flex deltas and session status. Approve or reject adjustment requests from the admin panel or via the one-click link in the notification email. Approving reconciles the submitted sessions against existing records and applies the correction immediately.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Monthly settlements</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">At the end of each month, generate a settlement per employee. The settlement summarises total hours worked, overtime, deficit, and lets you choose an outcome: <span class="font-medium text-slate-600 dark:text-slate-300">Paid</span>, <span class="font-medium text-slate-600 dark:text-slate-300">Leave Deducted</span>, or <span class="font-medium text-slate-600 dark:text-slate-300">Unpaid</span>. Confirmation is blocked if any open sessions or pending adjustment requests remain for that employee. Confirmed settlements are locked.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Payroll export</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">Export a CSV from the Settlements screen for any confirmed month. The file includes regular hours, overtime hours, total hours, outcome, and notes per employee — sourced directly from the confirmed settlement data.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Vacation types</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">Create and manage vacation types (name, color, description). Assign each type to employees with a yearly balance. Balances can be adjusted at any time.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">Public holidays</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">Set a country code to auto-import national holidays from an external source. Mark specific days as working if your company operates on them, or add custom holidays manually.</p>
                </div>

                <div class="border-t border-border" />

                <div class="space-y-0.5">
                  <p class="text-sm font-medium text-slate-700 dark:text-slate-300">App settings</p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">Set global defaults for per-weekday hour targets (Mon–Fri individually) and minimum break duration — both overridable per employee. Configure the notification email address and toggle whether missed clock-in reminders and adjustment request emails are sent.</p>
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
