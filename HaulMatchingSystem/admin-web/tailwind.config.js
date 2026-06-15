/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        "surface-container-low": "#eff4ff", "outline-variant": "#c4c5d5", "on-tertiary-container": "#ffa929",
        "on-primary-fixed-variant": "#173bab", "surface-container-lowest": "#ffffff", "surface-tint": "#3755c3",
        "surface": "#f8f9ff", "on-secondary-fixed": "#002113", "primary": "#00288e", "on-surface": "#0b1c30",
        "on-primary-container": "#a8b8ff", "error": "#ba1a1a", "on-tertiary-fixed-variant": "#653e00",
        "surface-container": "#e5eeff", "error-container": "#ffdad6", "secondary-container": "#6cf8bb",
        "on-primary": "#ffffff", "on-background": "#0b1c30", "surface-container-highest": "#d3e4fe",
        "primary-container": "#1e40af", "on-surface-variant": "#444653", "inverse-on-surface": "#eaf1ff",
        "tertiary": "#4c2e00", "background": "#f8f9ff", "secondary-fixed-dim": "#4edea3",
        "surface-container-high": "#dce9ff", "primary-fixed-dim": "#b8c4ff", "on-secondary-fixed-variant": "#005236",
        "tertiary-fixed-dim": "#ffb95f", "tertiary-container": "#6b4200", "on-primary-fixed": "#001453",
        "surface-variant": "#d3e4fe", "on-secondary-container": "#00714d", "on-tertiary": "#ffffff",
        "tertiary-fixed": "#ffddb8", "on-secondary": "#ffffff", "on-error": "#ffffff",
        "on-error-container": "#93000a", "secondary": "#006c49", "inverse-primary": "#b8c4ff",
        "on-tertiary-fixed": "#2a1700", "primary-fixed": "#dde1ff", "inverse-surface": "#213145",
        "surface-dim": "#cbdbf5", "secondary-fixed": "#6ffbbe", "outline": "#757684", "surface-bright": "#f8f9ff"
      },
      borderRadius: { "DEFAULT": "0.25rem", "lg": "0.5rem", "xl": "0.75rem", "full": "9999px" },
      spacing: { "stack-gap-md": "16px", "gutter": "16px", "container-margin": "24px", "unit": "8px", "stack-gap-sm": "8px", "card-padding": "20px" },
      fontFamily: {
        "headline-lg": ["Space Grotesk", "sans-serif"], "label-lg": ["Plus Jakarta Sans", "sans-serif"], "display-lg": ["Space Grotesk", "sans-serif"],
        "headline-md": ["Space Grotesk", "sans-serif"], "label-md": ["Plus Jakarta Sans", "sans-serif"], "body-lg": ["Plus Jakarta Sans", "sans-serif"],
        "headline-lg-mobile": ["Space Grotesk", "sans-serif"], "body-md": ["Plus Jakarta Sans", "sans-serif"],
        "sans": ["Plus Jakarta Sans", "sans-serif"], "display": ["Space Grotesk", "sans-serif"]
      }
    },
  },
  plugins: [],
}