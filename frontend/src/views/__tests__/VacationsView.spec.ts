import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, inject, provide, ref, toRef, type Ref } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { RouterView, createMemoryHistory, createRouter } from "vue-router";

// ─── Mocks ─────────────────────────────────────────────────────────────────────

const auth = vi.hoisted(() => ({
  isAdmin: null as unknown as Ref<boolean>,
  currentUser: null as unknown as Ref<{ id: string } | null>,
}));
vi.mock("@/composables/useAuth", async () => {
  const { ref } = await import("vue");
  auth.isAdmin = ref(false);
  auth.currentUser = ref({ id: "me" });
  return { useAuth: () => ({ isAdmin: auth.isAdmin, currentUser: auth.currentUser }) };
});

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock("@/composables/useAppToast", () => ({ useAppToast: () => toast }));

const vacations = vi.hoisted(() => ({
  getBalances: vi.fn(),
  getVacationDays: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  delete: vi.fn(),
  createRange: vi.fn(),
}));
vi.mock("@/services/vacationService", () => ({ vacationService: vacations }));

const requests = vi.hoisted(() => ({
  create: vi.fn(),
  getMine: vi.fn(),
  deleteDraft: vi.fn(),
  submit: vi.fn(),
  cancel: vi.fn(),
}));
vi.mock("@/services/vacationRequestService", () => ({ vacationRequestService: requests }));

vi.mock("@/services/holidayService", () => ({
  holidayService: { getHolidays: vi.fn(async () => []) },
}));
vi.mock("@/services/adminService", () => ({
  adminService: { getEmployees: vi.fn(async () => [{ id: "emp1", fullName: "Lotte Janssens" }]) },
}));
vi.mock("@/services/settlementService", () => ({
  settlementService: { getEmployeeHistory: vi.fn(async () => []) },
}));

import VacationsView from "../VacationsView.vue";
import { useConfirmDialog } from "@/composables/useConfirmDialog";

// ─── Stubs ─────────────────────────────────────────────────────────────────────
// The real popover and select render through portals and pointer events; these keep the same
// contract (open state, v-model) so the tests drive the page's own logic, not reka-ui.

const PopoverStub = defineComponent({
  props: { open: Boolean },
  emits: ["update:open"],
  setup(props, { slots, emit }) {
    provide("popoverOpen", toRef(props, "open"));
    provide("popoverToggle", () => emit("update:open", !props.open));
    return () => h("div", slots.default?.());
  },
});
const PopoverTriggerStub = defineComponent({
  setup(_, { slots }) {
    const toggle = inject<() => void>("popoverToggle")!;
    return () => h("div", { class: "trigger", onClick: toggle }, slots.default?.());
  },
});
const PopoverContentStub = defineComponent({
  setup(_, { slots }) {
    const open = inject<Ref<boolean>>("popoverOpen")!;
    return () => (open.value ? h("div", { class: "popover-content" }, slots.default?.()) : null);
  },
});
const Passthrough = defineComponent({
  setup:
    (_, { slots }) =>
    () =>
      h("div", slots.default?.()),
});

// ─── Fixtures ──────────────────────────────────────────────────────────────────

const ANNUAL = {
  vacationTypeId: 6,
  vacationTypeName: "Annual Leave",
  vacationTypeColor: "#3b82f6",
  isUnlimited: false,
  isAdminOnly: false,
  yearlyBalance: 20,
  usedDays: 0,
  pendingDays: 0,
  draftDays: 0,
  remainingDays: 20,
};

const draft = (over: Record<string, unknown> = {}) => ({
  id: 41,
  userId: "me",
  employeeName: "Me",
  vacationTypeId: 6,
  vacationTypeName: "Annual Leave",
  startDate: "2026-03-09",
  endDate: "2026-03-10",
  amount: 1,
  totalDays: 2,
  status: "Draft",
  requestedAt: "2026-03-01T10:00:00Z",
  reviewedAt: null,
  reviewedByName: null,
  reviewNote: null,
  ...over,
});

async function render({
  admin,
  employeeId,
  days = [],
  mine = [],
  balances = [ANNUAL],
}: {
  admin: boolean;
  employeeId?: string;
  days?: unknown[];
  mine?: unknown[];
  balances?: (typeof ANNUAL)[];
}) {
  auth.isAdmin.value = admin;
  vacations.getBalances.mockResolvedValue(balances);
  vacations.getVacationDays.mockResolvedValue(days);
  requests.getMine.mockResolvedValue(mine);

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/vacations", component: VacationsView },
      { path: "/other", component: { render: () => null } },
    ],
  });
  await router.push(employeeId ? `/vacations?employeeId=${employeeId}` : "/vacations");

  // Through a RouterView, as in the app: route guards such as the leave reminder only run there.
  const wrapper = mount(RouterView, {
    global: {
      plugins: [router],
      stubs: {
        Popover: PopoverStub,
        PopoverTrigger: PopoverTriggerStub,
        PopoverContent: PopoverContentStub,
        Select: Passthrough,
        SelectTrigger: Passthrough,
        SelectValue: Passthrough,
        SelectContent: Passthrough,
        SelectItem: Passthrough,
        YearCalendarOverlay: true,
      },
    },
  });
  await flushPromises();
  return { wrapper, router };
}

const openDay = async (wrapper: VueWrapper, iso: string) => {
  await wrapper.get(`[data-date="${iso}"]`).trigger("click");
  await flushPromises();
};

const button = (wrapper: VueWrapper, label: string | RegExp) => {
  const match = wrapper
    .findAll("button")
    .find((b) => (typeof label === "string" ? b.text().trim() === label : label.test(b.text())));
  if (!match)
    throw new Error(
      `No button "${label}" in: ${wrapper
        .findAll("button")
        .map((b) => b.text().trim())
        .join(" | ")}`
    );
  return match;
};

const hasButton = (wrapper: VueWrapper, label: string) =>
  wrapper.findAll("button").some((b) => b.text().trim() === label);

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-03-02T09:00:00"));
});

afterEach(() => {
  vi.useRealTimers();
  const dialog = useConfirmDialog();
  if (dialog.isOpen.value) dialog.handleCancel();
});

// ─── Admin: unchanged ──────────────────────────────────────────────────────────

describe("VacationsView as an admin (unchanged behaviour)", () => {
  it("books a day directly for themselves, without any request", async () => {
    const { wrapper } = await render({ admin: true });
    vacations.create.mockResolvedValue({
      id: 1,
      vacationTypeId: 6,
      vacationTypeName: "Annual Leave",
      date: "2026-03-04",
      amount: 1,
    });

    await openDay(wrapper, "2026-03-04");
    await button(wrapper, "Plan day").trigger("click");
    await flushPromises();

    expect(vacations.create).toHaveBeenCalledWith(
      { vacationTypeId: 6, date: "2026-03-04", amount: 1, note: undefined },
      undefined
    );
    expect(requests.create).not.toHaveBeenCalled();
    expect(requests.getMine).not.toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith("Vacation day planned");
    expect(wrapper.text()).toContain("Plan your vacation days");
    expect(wrapper.text()).not.toContain("Submit request");
  });

  it("books a range directly on behalf of an employee", async () => {
    const { wrapper } = await render({ admin: true, employeeId: "emp1" });
    vacations.createRange.mockResolvedValue({
      created: [],
      skippedWeekends: 0,
      skippedHolidays: 0,
      skippedExisting: 0,
    });

    await openDay(wrapper, "2026-03-04");
    const [, to] = wrapper.findAll('input[type="date"]');
    await to.setValue("2026-03-06");
    await button(wrapper, "Plan 3 day(s)").trigger("click");
    await flushPromises();

    expect(vacations.getBalances).toHaveBeenCalledWith(2026, "emp1");
    expect(vacations.createRange).toHaveBeenCalledWith(
      {
        vacationTypeId: 6,
        startDate: "2026-03-04",
        endDate: "2026-03-06",
        amount: 1,
        note: undefined,
      },
      "emp1"
    );
    expect(requests.create).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Manage vacation on behalf of Lotte Janssens");
  });

  it("can still edit an existing day", async () => {
    const day = {
      id: 7,
      vacationTypeId: 6,
      vacationTypeName: "Annual Leave",
      date: "2026-03-04",
      amount: 1,
    };
    const { wrapper } = await render({ admin: true, employeeId: "emp1", days: [day] });
    vacations.update.mockResolvedValue({ ...day, amount: 0.5 });

    await openDay(wrapper, "2026-03-04");
    expect(hasButton(wrapper, "Remove day")).toBe(false);
    await button(wrapper, "Save changes").trigger("click");
    await flushPromises();

    expect(vacations.update).toHaveBeenCalledWith(
      7,
      { vacationTypeId: 6, date: "2026-03-04", amount: 1, note: undefined },
      "emp1"
    );
  });

  it("previews the balance from booked days only, as before", async () => {
    const { wrapper } = await render({
      admin: true,
      employeeId: "emp1",
      balances: [{ ...ANNUAL, pendingDays: 3, draftDays: 2 }],
    });

    await openDay(wrapper, "2026-03-04");

    expect(wrapper.text()).toContain("19 day(s) remaining after this");
  });

  it("leaves the page without any prompt", async () => {
    const { router } = await render({ admin: true });

    await router.push("/other");

    expect(router.currentRoute.value.path).toBe("/other");
    expect(useConfirmDialog().isOpen.value).toBe(false);
  });
});

// ─── Employee: plan, then submit ───────────────────────────────────────────────

describe("VacationsView as an employee", () => {
  it("plans a draft instead of booking, and offers to submit it", async () => {
    const { wrapper } = await render({ admin: false });
    requests.create.mockResolvedValue(
      draft({ id: 50, startDate: "2026-03-04", endDate: "2026-03-04", totalDays: 1 })
    );

    await openDay(wrapper, "2026-03-04");
    await button(wrapper, "Plan day").trigger("click");
    await flushPromises();

    expect(requests.create).toHaveBeenCalledWith({
      vacationTypeId: 6,
      startDate: "2026-03-04",
      endDate: "2026-03-04",
      amount: 1,
      note: undefined,
    });
    expect(vacations.create).not.toHaveBeenCalled();
    expect(vacations.createRange).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("1 period planned");
    expect(wrapper.text()).toContain("Submit request");
  });

  it("submits all drafts after confirming the summary", async () => {
    const { wrapper } = await render({ admin: false, mine: [draft()] });
    requests.submit.mockResolvedValue([draft({ status: "Pending" })]);

    await button(wrapper, /Submit request/).trigger("click");
    const dialog = useConfirmDialog();
    expect(dialog.options.value?.message).toContain("2 days in total");
    expect(requests.submit).not.toHaveBeenCalled();

    await dialog.handleConfirm();
    await flushPromises();

    expect(requests.submit).toHaveBeenCalledOnce();
    expect(wrapper.text()).not.toContain("Submit request");
    expect(wrapper.text()).toContain("Awaiting approval");
  });

  it("holds draft and pending days against the balance it previews", async () => {
    const { wrapper } = await render({
      admin: false,
      balances: [{ ...ANNUAL, pendingDays: 3, draftDays: 2 }],
    });

    await openDay(wrapper, "2026-03-04");

    expect(wrapper.text()).toContain("14 day(s) remaining after this"); // 20 − 3 pending − 2 drafted − 1
  });

  it("can only remove an approved day, not edit it", async () => {
    const day = {
      id: 7,
      vacationTypeId: 6,
      vacationTypeName: "Annual Leave",
      date: "2026-03-04",
      amount: 1,
    };
    const { wrapper } = await render({ admin: false, days: [day] });

    await openDay(wrapper, "2026-03-04");

    expect(wrapper.text()).toContain("Approved vacation");
    expect(hasButton(wrapper, "Save changes")).toBe(false);
    expect(hasButton(wrapper, "Remove day")).toBe(true);
  });

  it("reminds once about unsent drafts when leaving, and keeps them on request", async () => {
    const { router } = await render({ admin: false, mine: [draft()] });

    await router.push("/other");
    const dialog = useConfirmDialog();
    expect(router.currentRoute.value.path).toBe("/vacations");
    expect(dialog.options.value?.cancelLabel).toBe("Keep as draft");

    dialog.handleCancel();
    await flushPromises();

    expect(router.currentRoute.value.path).toBe("/other");
    expect(requests.submit).not.toHaveBeenCalled();
    expect(requests.deleteDraft).not.toHaveBeenCalled();
  });

  it("can send the drafts from the leave reminder", async () => {
    const { router } = await render({ admin: false, mine: [draft()] });
    requests.submit.mockResolvedValue([draft({ status: "Pending" })]);

    await router.push("/other");
    await useConfirmDialog().handleConfirm();
    await flushPromises();

    expect(requests.submit).toHaveBeenCalledOnce();
    expect(router.currentRoute.value.path).toBe("/other");
  });
});
