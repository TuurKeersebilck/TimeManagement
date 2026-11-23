import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, inject, provide, toRef, type Ref } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";

const service = vi.hoisted(() => ({
  getNotifications: vi.fn(),
  getUnreadCount: vi.fn(),
  markAsRead: vi.fn(),
  markAllAsRead: vi.fn(),
  remove: vi.fn(),
}));
vi.mock("@/services/notificationService", () => ({ notificationService: service }));

// The real popover renders through a portal; this keeps its contract (open + update:open).
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
  props: { side: String, align: String },
  setup(props, { slots }) {
    const open = inject<Ref<boolean>>("popoverOpen")!;
    return () =>
      open.value ? h("div", { class: "panel", "data-side": props.side }, slots.default?.()) : null;
  },
});

const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString();
const list = () => [
  {
    id: 1,
    message: "Your Annual Leave request for 7 Dec has been approved",
    type: "VacationApproved",
    isRead: false,
    createdAt: minutesAgo(5),
  },
  {
    id: 2,
    message: "Lotte Janssens requested Annual Leave",
    type: "VacationRequest",
    isRead: true,
    createdAt: minutesAgo(60 * 30),
  },
];

async function render(props: Record<string, string> = {}) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/", component: { render: () => null } },
      { path: "/vacations", name: "vacations", component: { render: () => null } },
      {
        path: "/admin/vacation-requests",
        name: "admin-vacation-requests",
        component: { render: () => null },
      },
    ],
  });
  await router.push("/");
  const wrapper = mount((await import("../NotificationBell.vue")).default, {
    props,
    global: {
      plugins: [router],
      stubs: {
        Popover: PopoverStub,
        PopoverTrigger: PopoverTriggerStub,
        PopoverContent: PopoverContentStub,
      },
    },
  });
  await flushPromises();
  return { wrapper, router };
}

const openPanel = async (wrapper: VueWrapper) => {
  await wrapper.get(".trigger").trigger("click");
  await flushPromises();
};
const buttonNamed = (wrapper: VueWrapper, label: string) =>
  wrapper
    .findAll("button")
    .find((b) => b.text().trim() === label || b.attributes("aria-label") === label);

beforeEach(() => {
  vi.resetModules();
  service.getUnreadCount.mockReset().mockResolvedValue(1);
  service.getNotifications.mockReset().mockResolvedValue(list());
  service.markAsRead.mockReset().mockResolvedValue(undefined);
  service.markAllAsRead.mockReset().mockResolvedValue(undefined);
  service.remove.mockReset().mockResolvedValue(undefined);
});

afterEach(() => {
  document.body.innerHTML = "";
});

describe("NotificationBell", () => {
  it("shows the unread count on the bell without opening it", async () => {
    const { wrapper } = await render();

    expect(wrapper.get("button[aria-label='Notifications, 1 unread']").text()).toBe("1");
    expect(service.getNotifications).not.toHaveBeenCalled();
  });

  it("opens with titles, short times and Today/Earlier groups", async () => {
    const { wrapper } = await render();

    await openPanel(wrapper);

    const text = wrapper.get(".panel").text();
    expect(text).toContain("Today");
    expect(text).toContain("Earlier");
    expect(text).toContain("Vacation approved");
    expect(text).toContain("5 min ago");
    expect(text).toContain("Vacation request");
    expect(text).toContain("Lotte Janssens requested Annual Leave");
  });

  it("filters to unread, and says so when there are none left", async () => {
    const { wrapper } = await render();
    await openPanel(wrapper);

    await wrapper.findAll("[role=tab]")[1].trigger("click");
    expect(wrapper.get(".panel").text()).toContain("Vacation approved");
    expect(wrapper.get(".panel").text()).not.toContain("Lotte Janssens requested");

    await buttonNamed(wrapper, "Mark all as read")!.trigger("click");
    await flushPromises();
    expect(wrapper.get(".panel").text()).toContain("You're all caught up");
    expect(service.markAllAsRead).toHaveBeenCalledOnce();
  });

  it("marks as read and goes to the related page when clicked", async () => {
    const { wrapper, router } = await render();
    await openPanel(wrapper);

    const item = wrapper.findAll("li").find((li) => li.text().includes("Vacation approved"))!;
    await item.find("button").trigger("click");
    await flushPromises();

    expect(service.markAsRead).toHaveBeenCalledWith(1);
    expect(router.currentRoute.value.name).toBe("vacations");
    expect(wrapper.find(".panel").exists()).toBe(false);
  });

  it("removes one without opening it", async () => {
    const { wrapper, router } = await render();
    await openPanel(wrapper);

    const item = wrapper.findAll("li").find((li) => li.text().includes("Vacation request"))!;
    await item.get("button[aria-label='Remove']").trigger("click");
    await flushPromises();

    expect(service.remove).toHaveBeenCalledWith(2);
    expect(wrapper.get(".panel").text()).not.toContain("Lotte Janssens requested");
    expect(router.currentRoute.value.path).toBe("/");
  });

  it("shows an empty state when there is nothing at all", async () => {
    service.getUnreadCount.mockResolvedValue(0);
    service.getNotifications.mockResolvedValue([]);
    const { wrapper } = await render();

    await openPanel(wrapper);

    expect(wrapper.get(".panel").text()).toContain("No notifications yet");
  });

  it("opens beside the sidebar on a wide screen", async () => {
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("min-width: 1024px"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    try {
      const { wrapper } = await render();
      await openPanel(wrapper);
      expect(wrapper.get(".panel").attributes("data-side")).toBe("right");
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("always opens downward on a narrow screen, even from the sidebar", async () => {
    // A sideways panel from the sidebar would run off a phone screen.
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    try {
      const { wrapper } = await render({ side: "right" });
      await openPanel(wrapper);
      expect(wrapper.get(".panel").attributes("data-side")).toBe("bottom");
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("stops checking for new ones once it's gone", async () => {
    vi.useFakeTimers();
    try {
      const { wrapper } = await render();
      const calls = service.getUnreadCount.mock.calls.length;

      wrapper.unmount();
      await vi.advanceTimersByTimeAsync(5 * 60_000);

      expect(service.getUnreadCount.mock.calls.length).toBe(calls);
    } finally {
      vi.useRealTimers();
    }
  });
});
