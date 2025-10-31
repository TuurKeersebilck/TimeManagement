import { definePreset } from "@primeuix/themes";
import Aura from "@primeuix/themes/aura";

const Noir = definePreset(Aura, {
	semantic: {
		primary: {
			50: "#eef2ff", // indigo-50
			100: "#e0e7ff", // indigo-100
			200: "#c7d2fe", // indigo-200
			300: "#a5b4fc", // indigo-300
			400: "#818cf8", // indigo-400
			500: "#6366f1", // indigo-500
			600: "#4f46e5", // indigo-600
			700: "#4338ca", // indigo-700
			800: "#3730a3", // indigo-800
			900: "#312e81", // indigo-900
			950: "#1e1b4b", // indigo-950
		},
		colorScheme: {
			light: {
				primary: {
					color: "{primary.600}",
					contrastColor: "#ffffff",
					hoverColor: "{primary.700}",
					activeColor: "{primary.800}",
				},
				highlight: {
					background: "rgba(99, 102, 241, 0.1)",
					focusBackground: "{primary.600}",
					color: "{primary.700}",
					focusColor: "#ffffff",
				},
				surface: {
					0: "#ffffff",
					50: "#f8fafc", // slate-50
					100: "#f1f5f9", // slate-100
					200: "#e2e8f0", // slate-200
					300: "#cbd5e1", // slate-300
					400: "#94a3b8", // slate-400
					500: "#64748b", // slate-500
					600: "#475569", // slate-600
					700: "#334155", // slate-700
					800: "#1e293b", // slate-800
					900: "#0f172a", // slate-900
					950: "#020617", // slate-950
				},
			},
			dark: {
				primary: {
					color: "{primary.400}",
					contrastColor: "{primary.950}",
					hoverColor: "{primary.300}",
					activeColor: "{primary.200}",
				},
				highlight: {
					background: "rgba(129, 140, 248, 0.16)",
					focusBackground: "{primary.400}",
					color: "{primary.300}",
					focusColor: "{primary.950}",
				},
				surface: {
					0: "#0f172a", // slate-900
					50: "#1e293b", // slate-800
					100: "#334155", // slate-700
					200: "#475569", // slate-600
					300: "#64748b", // slate-500
					400: "#94a3b8", // slate-400
					500: "#cbd5e1", // slate-300
					600: "#e2e8f0", // slate-200
					700: "#f1f5f9", // slate-100
					800: "#f8fafc", // slate-50
					900: "#ffffff",
					950: "#ffffff",
				},
			},
		},
	},
	components: {
		button: {
			colorScheme: {
				light: {
					primary: {
						background: "{primary.600}",
						hoverBackground: "{primary.700}",
						activeBackground: "{primary.800}",
						borderColor: "{primary.600}",
						hoverBorderColor: "{primary.700}",
						color: "#ffffff",
						hoverColor: "#ffffff",
						activeColor: "#ffffff",
					},
				},
			},
		},
		datatable: {
			colorScheme: {
				light: {
					headerCell: {
						background: "{surface.50}",
						hoverBackground: "{surface.100}",
						borderColor: "{surface.200}",
						color: "{surface.700}",
						hoverColor: "{surface.800}",
					},
					row: {
						background: "#ffffff",
						hoverBackground: "{surface.50}",
						selectedBackground: "rgba(99, 102, 241, 0.1)",
						selectedHoverBackground: "rgba(99, 102, 241, 0.15)",
						color: "{surface.700}",
						hoverColor: "{surface.800}",
					},
				},
			},
		},
		toast: {
			colorScheme: {
				light: {
					success: {
						background: "#f0fdf4",
						borderColor: "#86efac",
						color: "#166534",
						detailColor: "#15803d",
						shadow: "0px 4px 8px 0px rgba(34, 197, 94, 0.04)",
					},
					error: {
						background: "#fef2f2",
						borderColor: "#fecaca",
						color: "#991b1b",
						detailColor: "#b91c1c",
						shadow: "0px 4px 8px 0px rgba(239, 68, 68, 0.04)",
					},
					warn: {
						background: "#fffbeb",
						borderColor: "#fde68a",
						color: "#92400e",
						detailColor: "#b45309",
						shadow: "0px 4px 8px 0px rgba(245, 158, 11, 0.04)",
					},
					info: {
						background: "#eff6ff",
						borderColor: "#bfdbfe",
						color: "#1e40af",
						detailColor: "#1e3a8a",
						shadow: "0px 4px 8px 0px rgba(59, 130, 246, 0.04)",
					},
				},
			},
		},
	},
});

export default Noir;
