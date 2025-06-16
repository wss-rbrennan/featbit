docker build -t featbit-els-edge:dev  -f deploy/Edge/Dockerfile .
docker build -t featbit-els-hub:dev  -f deploy/Hub/Dockerfile .
docker build -t featbit-els-web:dev  -f deploy/Web/Dockerfile .