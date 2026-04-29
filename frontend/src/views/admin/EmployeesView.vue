<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type Employee } from "../../services/adminService";
import { inviteService, type Invite } from "../../services/inviteService";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { UsersIcon, ChevronRightIcon, MailIcon, Loader2Icon, XIcon } from "lucide-vue-next";

const toast = useAppToast();
const router = useRouter();
const employees = ref<Employee[]>([]);
const loading = ref(false);

const pendingInvites = ref<Invite[]>([]);
const invitesLoading = ref(false);

const showInviteForm = ref(false);
const inviteEmail = ref("");
const inviteLoading = ref(false);

const weekStatus = (emp: Employee): "on-track" | "behind" | "none" => {
  if (emp.resolvedWeeklyTarget == null) return "none";
  return emp.weeklyHoursLogged >= emp.resolvedWeeklyTarget ? "on-track" : "behind";
};

function formatExpiresAt(isoString: string): string {
  const d = new Date(isoString);
  return d.toLocaleDateString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

onMounted(async () => {
  loading.value = true;
  invitesLoading.value = true;
  try {
    [employees.value, pendingInvites.value] = await Promise.all([
      adminService.getEmployees(),
      inviteService.getPendingInvites(),
    ]);
  } catch {
    toast.error("Failed to load employees");
  } finally {
    loading.value = false;
    invitesLoading.value = false;
  }
});

async function sendInvite() {
  if (!inviteEmail.value.trim()) return;
  inviteLoading.value = true;
  try {
    const invite = await inviteService.createInvite(inviteEmail.value.trim());
    pendingInvites.value.unshift(invite);
    inviteEmail.value = "";
    showInviteForm.value = false;
    toast.success("Invitation sent");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to send invite"));
  } finally {
    inviteLoading.value = false;
  }
}

async function cancelInvite(invite: Invite) {
  try {
    await inviteService.cancelInvite(invite.id);
    pendingInvites.value = pendingInvites.value.filter((i) => i.id !== invite.id);
    toast.success("Invitation cancelled");
  } catch {
    toast.error("Failed to cancel invite");
  }
}
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-4xl mx-auto space-y-8">
        <!-- Employees section -->
        <div>
          <div class="flex items-center justify-between mb-6">
            <div>
              <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Employees</h1>
              <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Click an employee to manage their vacation types, balance and hours targets</p>
            </div>
            <Button
              v-if="!showInviteForm"
              size="sm"
              @click="showInviteForm = true"
            >
              <MailIcon class="size-4 mr-1.5" />
              Invite employee
            </Button>
          </div>

          <!-- Inline invite form -->
          <div
            v-if="showInviteForm"
            class="card p-4 mb-4 flex items-end gap-3"
          >
            <div class="flex-1 space-y-1.5">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Email address</label>
              <Input
                v-model="inviteEmail"
                type="email"
                placeholder="employee@company.com"
                autofocus
                @keydown.enter.prevent="sendInvite"
              />
            </div>
            <Button @click="sendInvite" :disabled="inviteLoading || !inviteEmail.trim()">
              <Loader2Icon v-if="inviteLoading" class="size-4 animate-spin mr-1.5" />
              Send invite
            </Button>
            <Button variant="ghost" @click="showInviteForm = false; inviteEmail = ''">
              Cancel
            </Button>
          </div>

          <div class="card overflow-hidden">
            <!-- Loading skeleton -->
            <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
              <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-36 animate-pulse" />
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-48 animate-pulse" />
              </div>
            </div>

            <Table v-else>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>This week</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableEmpty v-if="employees.length === 0" :colspan="4">
                  <UsersIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                  <p class="text-slate-500 dark:text-slate-400">No employees found.</p>
                </TableEmpty>
                <TableRow
                  v-for="employee in employees"
                  :key="employee.id"
                  class="cursor-pointer hover:bg-muted/50 transition-colors"
                  @click="router.push({ name: 'admin-employee-detail', params: { id: employee.id } })"
                >
                  <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                    {{ employee.fullName }}
                  </TableCell>
                  <TableCell class="text-slate-600 dark:text-slate-400">
                    {{ employee.email }}
                  </TableCell>
                  <TableCell>
                    <div class="flex items-center gap-2">
                      <span class="text-sm text-slate-700 dark:text-slate-300">
                        {{ employee.weeklyHoursLogged.toFixed(1) }}h
                        <span v-if="employee.resolvedWeeklyTarget != null" class="text-slate-400 dark:text-slate-500">
                          / {{ employee.resolvedWeeklyTarget }}h
                        </span>
                      </span>
                      <span
                        v-if="weekStatus(employee) !== 'none'"
                        :class="[
                          'inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium',
                          weekStatus(employee) === 'on-track'
                            ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                            : 'bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-300',
                        ]"
                      >
                        {{ weekStatus(employee) === "on-track" ? "On track" : "Behind" }}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell class="text-right w-8">
                    <ChevronRightIcon class="size-4 text-slate-400 dark:text-slate-500 ml-auto" />
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </div>

        <!-- Pending invitations section -->
        <div v-if="invitesLoading || pendingInvites.length > 0">
          <div class="mb-4">
            <h2 class="text-base font-semibold text-slate-900 dark:text-slate-100">Pending invitations</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Invites expire 48 hours after being sent</p>
          </div>

          <div class="card overflow-hidden">
            <div v-if="invitesLoading" class="divide-y divide-slate-100 dark:divide-slate-800">
              <div v-for="i in 2" :key="i" class="flex items-center gap-4 px-4 py-3.5">
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-48 animate-pulse" />
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
              </div>
            </div>

            <Table v-else>
              <TableHeader>
                <TableRow>
                  <TableHead>Email</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow
                  v-for="invite in pendingInvites"
                  :key="invite.id"
                >
                  <TableCell class="text-slate-700 dark:text-slate-300">
                    {{ invite.email }}
                  </TableCell>
                  <TableCell class="text-sm text-slate-500 dark:text-slate-400">
                    {{ formatExpiresAt(invite.expiresAt) }}
                  </TableCell>
                  <TableCell class="text-right">
                    <button
                      class="p-1 rounded hover:bg-destructive/10 text-slate-400 hover:text-destructive transition-colors cursor-pointer"
                      title="Cancel invite"
                      @click="cancelInvite(invite)"
                    >
                      <XIcon class="size-4" />
                    </button>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
