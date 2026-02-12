# Build Stage
FROM node:20 AS build
WORKDIR /app
COPY ["websites/komorii/package.json", "websites/komorii/package-lock.json", "./"]
RUN npm install
COPY ["websites/komorii/", "./"]
RUN npm run build

# Serve Stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
