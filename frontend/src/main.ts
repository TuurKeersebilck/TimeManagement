import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import "./style.css";
import PrimeVue from "primevue/config";
import ToastService from "primevue/toastservice";
import Noir from "./presets/Noir";

createApp(App)
	.use(router)
	.use(PrimeVue, {
		theme: {
			preset: Noir,
			options: {
				prefix: "p",
				darkModeSelector: ".dark",
				cssLayer: false,
			},
		},
	})
	.use(ToastService)
	.mount("#app");
