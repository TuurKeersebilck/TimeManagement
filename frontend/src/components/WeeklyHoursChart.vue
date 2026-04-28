<script setup lang="ts">
import { computed } from "vue";
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale,
  LineElement,
  PointElement,
  type ChartData,
  type ChartOptions,
} from "chart.js";
import { Bar } from "vue-chartjs";
import type { WeekSummary } from "@/services/adminService";

ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  LineElement,
  PointElement,
  Title,
  Tooltip,
  Legend
);

const props = defineProps<{
  weeks: WeekSummary[];
  isDark?: boolean;
}>();

const textColor = computed(() => (props.isDark ? "#94a3b8" : "#64748b"));
const gridColor = computed(() => (props.isDark ? "#1e293b" : "#f1f5f9"));

const chartData = computed<ChartData<"bar">>(() => ({
  labels: props.weeks.map((w) => w.weekLabel),
  datasets: [
    {
      label: "Logged",
      data: props.weeks.map((w) => w.hoursLogged),
      backgroundColor: props.weeks.map((w) => {
        if (!w.target) return "#6366f1";
        return w.hoursLogged >= w.target ? "#10b981" : "#f59e0b";
      }),
      borderRadius: 4,
      borderSkipped: false,
    },
    ...(props.weeks[0]?.target != null
      ? [
          {
            label: "Target",
            data: props.weeks.map((w) => w.target ?? 0),
            type: "line" as const,
            borderColor: "#6366f1",
            borderWidth: 2,
            borderDash: [4, 4],
            pointRadius: 0,
            fill: false,
            tension: 0,
          } as never,
        ]
      : []),
  ],
}));

const chartOptions = computed<ChartOptions<"bar">>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      labels: {
        color: textColor.value,
        boxWidth: 12,
        font: { size: 11 },
      },
    },
    tooltip: {
      callbacks: {
        label: (ctx) => ` ${ctx.dataset.label}: ${ctx.parsed.y.toFixed(2)}h`,
      },
    },
  },
  scales: {
    x: {
      ticks: { color: textColor.value, font: { size: 11 } },
      grid: { color: gridColor.value },
    },
    y: {
      ticks: {
        color: textColor.value,
        font: { size: 11 },
        callback: (v) => `${v}h`,
      },
      grid: { color: gridColor.value },
      beginAtZero: true,
    },
  },
}));
</script>

<template>
  <div class="relative h-52 overflow-hidden">
    <Bar :data="chartData" :options="chartOptions" />
  </div>
</template>
