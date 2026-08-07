# src/TsiBroker.Ru.Mock.UI

Vue 3 console for `TsiBroker.Ru.Mock` (the EVU/RU test double): configure its response behaviour,
send messages, and inspect the message log. Shares design tokens/components and the
username/password cookie-auth pattern with [TsiBroker.Ui](../TsiBroker.Ui), the admin UI, and
mirrors [TsiBroker.Im.Mock.UI](../TsiBroker.Im.Mock.UI) (the ISB test double's console) for the
IM side of the same message flow.

## Project Setup

```sh
npm install
```

### Compile and Hot-Reload for Development

```sh
npm run dev
```

### Type-Check, Compile and Minify for Production

```sh
npm run build
```

### Lint with [ESLint](https://eslint.org/)

```sh
npm run lint
```
