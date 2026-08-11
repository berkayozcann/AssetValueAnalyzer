import { copyFile, mkdir } from "node:fs/promises";

const copies = [
  [
    "./node_modules/@fontsource-variable/inter/files/inter-latin-wght-normal.woff2",
    "./wwwroot/fonts/inter-latin-wght-normal.woff2",
  ],
  [
    "./node_modules/@fontsource-variable/inter/files/inter-latin-ext-wght-normal.woff2",
    "./wwwroot/fonts/inter-latin-ext-wght-normal.woff2",
  ],
  [
    "./node_modules/@fontsource-variable/manrope/files/manrope-latin-wght-normal.woff2",
    "./wwwroot/fonts/manrope-latin-wght-normal.woff2",
  ],
  [
    "./node_modules/@fontsource-variable/manrope/files/manrope-latin-ext-wght-normal.woff2",
    "./wwwroot/fonts/manrope-latin-ext-wght-normal.woff2",
  ],
  [
    "./node_modules/@fontsource-variable/inter/LICENSE",
    "./wwwroot/fonts/licenses/inter-OFL.txt",
  ],
  [
    "./node_modules/@fontsource-variable/manrope/LICENSE",
    "./wwwroot/fonts/licenses/manrope-OFL.txt",
  ],
  [
    "./node_modules/@microsoft/signalr/dist/browser/signalr.min.js",
    "./wwwroot/lib/signalr/signalr.min.js",
  ],
];

await Promise.all([
  mkdir("./wwwroot/fonts/licenses", { recursive: true }),
  mkdir("./wwwroot/lib/signalr", { recursive: true }),
]);

await Promise.all(copies.map(([source, destination]) => copyFile(source, destination)));
