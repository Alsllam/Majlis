"""Cloud adapter: Azure Blob Storage (Azurite locally). The only module importing the storage SDK."""

from azure.identity.aio import DefaultAzureCredential
from azure.storage.blob.aio import BlobServiceClient

from ai_service.settings import Settings


class AzureBlobStore:
    def __init__(self, settings: Settings) -> None:
        if settings.azure_storage_connection_string is not None:
            self._client = BlobServiceClient.from_connection_string(
                settings.azure_storage_connection_string.get_secret_value()
            )
        elif settings.azure_storage_account_url:
            # Managed identity with Storage Blob Data Reader.
            self._client = BlobServiceClient(settings.azure_storage_account_url, credential=DefaultAzureCredential())
        else:
            raise RuntimeError("AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT_URL is required.")
        self._container = settings.blob_container

    async def download(self, path: str) -> bytes:
        blob = self._client.get_container_client(self._container).get_blob_client(path)
        stream = await blob.download_blob()
        return await stream.readall()

    async def aclose(self) -> None:
        await self._client.close()
