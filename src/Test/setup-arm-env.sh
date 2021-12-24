sudo apt update && sudo apt install -y qemu qemu-user-static qemu-user binfmt-support && docker run --rm --privileged multiarch/qemu-user-static --reset -p yes

docker buildx create --name testcontainer && docker buildx use testcontainer

docker buildx build --file "$(Build.SourcesDirectory)/src/DockerFile-${{ parameters.arch }}" --platform linux/${{ parameters.os }}-${{ parameters.arch }} --load -t test-${{ parameters.arch }} .
