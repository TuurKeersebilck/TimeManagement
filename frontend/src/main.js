import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import PrimeVue from "primevue/config";
import Aura from '@primeuix/themes/aura';
import { definePreset } from '@primeuix/themes';

// PrimeVue CSS
import "primeicons/primeicons.css";
import "primeflex/primeflex.css";
import "./index.css";

// Custom theme preset for time management
const TimeTheme = definePreset(Aura, {
    semantic: {
        primary: {
            50: '{indigo.50}',
            100: '{indigo.100}',
            200: '{indigo.200}',
            300: '{indigo.300}',
            400: '{indigo.400}',
            500: '{indigo.500}',
            600: '{indigo.600}',
            700: '{indigo.700}',
            800: '{indigo.800}',
            900: '{indigo.900}',
            950: '{indigo.950}'
        },
        colorScheme: {
            light: {
                primary: {
                    color: '{indigo.600}',
                    inverseColor: '#ffffff',
                    hoverColor: '{indigo.700}',
                    activeColor: '{indigo.800}'
                },
                highlight: {
                    background: '{indigo.50}',
                    focusBackground: '{indigo.100}',
                    color: '{indigo.700}',
                    focusColor: '{indigo.800}'
                },
                surface: {
                    0: '#ffffff',
                    50: '{slate.50}',
                    100: '{slate.100}',
                    200: '{slate.200}',
                    300: '{slate.300}',
                    400: '{slate.400}',
                    500: '{slate.500}',
                    600: '{slate.600}',
                    700: '{slate.700}',
                    800: '{slate.800}',
                    900: '{slate.900}',
                    950: '{slate.950}'
                }
            },
            dark: {
                primary: {
                    color: '{indigo.400}',
                    inverseColor: '{indigo.950}',
                    hoverColor: '{indigo.300}',
                    activeColor: '{indigo.200}'
                },
                highlight: {
                    background: 'rgba(99, 102, 241, 0.16)',
                    focusBackground: 'rgba(99, 102, 241, 0.24)',
                    color: 'rgba(165, 180, 252, 0.87)',
                    focusColor: 'rgba(165, 180, 252, 1)'
                },
                surface: {
                    0: '#0f172a',
                    50: '{slate.50}',
                    100: '{slate.100}',
                    200: '{slate.200}',
                    300: '{slate.300}',
                    400: '{slate.400}',
                    500: '{slate.500}',
                    600: '{slate.600}',
                    700: '{slate.700}',
                    800: '{slate.800}',
                    900: '{slate.900}',
                    950: '{slate.950}'
                }
            }
        }
    }
});

const app = createApp(App);
app.use(router);
app.use(PrimeVue, {
    theme: {
        preset: TimeTheme,
        options: {
            prefix: 'p',
            darkModeSelector: '.dark-mode',
            cssLayer: false
        }
    },
    ripple: true
});

app.mount("#app");