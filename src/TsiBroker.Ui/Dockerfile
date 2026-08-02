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

COPY package.json package-lock.json ./
RUN npm install

COPY . .
RUN npm run build

# Runtime stage
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
