# Build Stage
FROM node:20 AS build
WORKDIR /app
COPY ["websites/index-admin-ui/package.json", "websites/index-admin-ui/package-lock.json", "./"]
RUN npm install
COPY ["websites/index-admin-ui/", "./"]
RUN npm run build

# Serve Stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
