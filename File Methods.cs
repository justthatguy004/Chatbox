namespace Chhatbox_type_shii
{
    public class Files
    {
        public async Task Copy(string sourceFilePath, string destinationDirectory)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string copiedFilePath = Path.Combine(destinationDirectory, fileName);
            FileInfo source = new FileInfo(sourceFilePath);
            if (File.Exists(copiedFilePath))
            {
                string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
                string ext = Path.GetExtension(sourceFilePath);
                copiedFilePath = Path.Combine(destinationDirectory, fileName);

                int counter = 1;

                while (File.Exists(copiedFilePath))
                {
                    copiedFilePath = Path.Combine(destinationDirectory,
                        $"{baseName} (Copy{counter}){ext}");
                    counter++;
                }
            }
            using (FileStream sourceFile = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
            using (FileStream destinationFile = new FileStream(copiedFilePath, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[81920];
                int bytesRead = 0;
                long progress = 0;
                long totalBytes = sourceFile.Length;
                while((bytesRead = await sourceFile.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destinationFile.WriteAsync(buffer, 0, bytesRead);
                    progress += bytesRead;
                    Console.Write($"\rProgress: {(double)progress / totalBytes:P2}");
                }
                Console.WriteLine("\nFile copied Successfully");
            }
        }
    }
}
