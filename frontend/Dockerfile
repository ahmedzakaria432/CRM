# Nuxt app (customer portal SSR + agent/admin SPA). Build context: frontend/.

FROM node:24-alpine AS build
WORKDIR /app
RUN corepack enable

# Install layer: lockfile only. postinstall (nuxt prepare) needs the sources, and nuxt build runs it anyway.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile --ignore-scripts

COPY . .
RUN pnpm build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    HOST=0.0.0.0 \
    PORT=3000
COPY --from=build /app/.output ./.output
USER node
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
