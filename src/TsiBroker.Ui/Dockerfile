# Build stage
FROM node:22-alpine AS build
WORKDIR /app

# Baked into the JS bundle at build time (Vite env vars aren't readable at container
# runtime). Leave unset to use same-origin relative /api calls, which is what the
# nginx reverse proxy below expects.
ARG VITE_API_BASE_URL=
ARG VITE_THEME_COLOR=
ARG VITE_DEFAULT_LOCALE=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_THEME_COLOR=$VITE_THEME_COLOR
ENV VITE_DEFAULT_LOCALE=$VITE_DEFAULT_LOCALE

# Build context is the repo root (see docker-compose.yml) because this app depends on
# the @tsibroker/ui-kit workspace package in ../../packages/tsibroker-ui-kit — npm
# needs every workspace's package.json up front to resolve the workspace, and the
# kit's source has to be present for Vite to compile it into this app's bundle.
#
# Deliberately install the whole workspace here, not `--workspace=src/TsiBroker.Ui`-scoped:
# scoped installs were observed to silently skip devDependencies (e.g. npm-run-all2, which
# `npm run build` needs) for versions that don't hoist to the root node_modules. The extra
# install time for the sibling workspace is negligible.
#
# Deliberately NOT `npm ci` against the committed package-lock.json, and deliberately not
# even copying it in: that lockfile is authored on Windows dev machines, and npm has a
# long-standing bug (https://github.com/npm/cli/issues/4828) where it only records
# `packages[...]` metadata for the authoring OS/libc — so native-binary optional deps (e.g.
# rolldown's `@rolldown/binding-linux-x64-musl`, needed by Vite in this Alpine image) are
# missing from it and `vite build` fails with "Cannot find native binding", even under a
# plain `npm install` (it still trusts the lockfile's existing package entries and won't
# add the missing platform's). Resolving fresh from package.json inside the container
# — its actual platform — avoids that at the cost of exact reproducibility for this layer.
COPY packages/tsibroker-ui-kit/package.json packages/tsibroker-ui-kit/
COPY src/TsiBroker.Ui/package.json src/TsiBroker.Ui/
COPY src/TsiBroker.Im.Mock.UI/package.json src/TsiBroker.Im.Mock.UI/
COPY package.json ./
RUN npm install

COPY packages/tsibroker-ui-kit packages/tsibroker-ui-kit
COPY src/TsiBroker.Ui src/TsiBroker.Ui
RUN npm run build --workspace=src/TsiBroker.Ui

# Runtime stage
FROM nginx:alpine AS final
COPY --from=build /app/src/TsiBroker.Ui/dist /usr/share/nginx/html
COPY src/TsiBroker.Ui/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
