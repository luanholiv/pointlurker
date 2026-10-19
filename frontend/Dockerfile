# syntax=docker/dockerfile:1

# Runtime-only image — expects pre-built dist/ and nginx.conf as build context
FROM nginx:1.31-alpine
LABEL org.opencontainers.image.source="https://github.com/luanholiv/pointlurker"

# Each package here carries a high-severity advisory that Alpine has already fixed,
# but the nginx base image has not been rebuilt to pick it up — so the release
# container scan fails on it. Upgrading just those packages is what unblocks that;
# drop each one once the base ships its fixed version.
RUN apk upgrade --no-cache libexpat tiff zlib

# The base image's `user` directive is dropped because this image runs unprivileged.
# A non-root master cannot assign worker users, so nginx warns that the directive was
# ignored on every start — noise that reads as a misconfiguration but is not one.
RUN rm /etc/nginx/conf.d/default.conf && \
    sed -i '/^user[[:space:]]/d' /etc/nginx/nginx.conf

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY dist/ /usr/share/nginx/html

# Everything nginx writes at runtime has to be owned by the user it now runs as: the
# static root, the proxy/client temp dirs under /var/cache/nginx that the /api
# reverse proxy buffers through, the log symlinks, and the pid file named by the base
# image's nginx.conf.
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid

# The uid, not the name: a name resolves only inside the image. 101 is the uid the base
# image gives nginx, and Docker still takes the primary group from /etc/passwd, so this is
# the same nginx:nginx the chowns above target.
USER 101

# 8080, not 80: an unprivileged process cannot bind a privileged port. The published
# host port is unchanged — compose maps host 80 onto this — so only the in-container
# listener moved. Kept in step with the `listen` directive in nginx.conf.
#
# The image still advertises 80 as well, because EXPOSE is additive and the base image
# declares it; there is no way to withdraw an inherited port. Nothing listens there, so
# `docker run -P` would publish one dead port. Compose maps this one explicitly.
EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
