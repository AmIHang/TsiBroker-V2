podman compose --env-file .env.example up -d --build

# Broker + ISB mock
podman compose --env-file .env.example --profile im-mock up -d --build

# Broker + ISB mock + EVU mock
podman compose --env-file .env.example --profile im-mock --profile ru-mock up -d --build
