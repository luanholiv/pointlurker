# syntax=docker/dockerfile:1

# Runtime-only image — expects pre-built publish output as build context (amd64/ and arm64/ subdirs)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
LABEL org.opencontainers.image.source="https://github.com/luanholiv/pointlurker"

WORKDIR /app

RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S appuser -G appgroup

# krb5-libs provides libgssapi_krb5.so.2, which the runtime's GSS shim dlopen()s on the
# first Npgsql connection of every process: nothing here authenticates with Kerberos, but
# a failed load prints two lines straight to stderr from native code, where no logging
# configuration can reach them and every collector tags them as errors. The package is here
# so that probe succeeds silently.
#
# icu-libs carries the culture data the outbound emails need. Alpine ships no ICU, so the
# base image declares globalization-invariant mode, under which constructing ANY named
# culture throws — and the notification mail constructs one to pick its translation and
# formats. Without this the application starts, serves every request, and fails only when
# it tries to send mail, which no request is waiting on.
#
# Both pins are fuzzy, not exact. Alpine's index carries only the current revision, so
# krb5-libs=1.22.2-r1 starts failing the day Alpine ships -r2, and Renovate has no apk
# datasource to bump it — worst of all here, on a base left deliberately floating so the
# monthly security rebuild lands with no maintainer action. Each prefix stops at the
# component that carries the soname, so a rebuild flows in while an unexpected minor or
# major jump fails and gets reviewed.
RUN apk add --no-cache 'krb5-libs=~1.22' 'icu-libs=~78'

# Overrides the base image, which sets this true because Alpine has no ICU by default.
# Installing icu-libs above is not enough on its own: this variable forces invariant mode
# whatever is on disk.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ARG TARGETARCH
COPY ${TARGETARCH}/ .

RUN chmod +x ./Presentation.WebApi && chown -R appuser:appgroup /app

# The uid, not the name: a name resolves only inside the image, and the keyring volume is
# chowned to this uid by the init sidecar in docker-compose.yml — the two 1000s are the
# same number and now read as one. Docker still takes the primary group from /etc/passwd,
# so this is the appuser:appgroup the chown above targets.
USER 1000

EXPOSE 8080

ENTRYPOINT ["./Presentation.WebApi"]
