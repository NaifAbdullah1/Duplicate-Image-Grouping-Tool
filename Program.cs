﻿/*
TODO: 
1- Do some further testing with real life images

2- Once it's confirmed that there are no further major errors occurring, implement support for other image types, escpecially proprietary format such as HEIF and HEIC. 

3- Complete the PDF writeup: This PDF will write a huge table listing all the similarity scores. It will also list unique images and unsupported images. 

4- Revise the Github page for any potential improvements

5- Learn how to deploy this as a console application. 

6- Consider adding a mechanism to allow the user to control the sensitivity or the similarity score. 

7- Look into ways to improve runtime. Perhaps there's a way to run the program asynchronously. 

- Clean the code and ensure you have javadoc and inline comments
*/


using System.Drawing;
using System.Drawing.Imaging;

using iText.Kernel.Pdf;
using iText.Layout;
using System.Text.RegularExpressions;



namespace DuplicateImageDeletionTool
{
    /// <summary>
    /// This class contains the main program logic for removing duplicated 
    /// images from a specified directory. It prompts the user for an 
    /// absolute path, validates and sanitizes the input to prevent 
    /// attacks, and then proceeds with duplicate removal.
    /// </summary>
    class Program
    {
        /// <summary>
        /// The `Main` method prompts the user for an absolute path, 
        /// validates and sanitizes the input, and then initiates the
        /// process of removing duplicated images from the specified directory.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static void Main(string[] args)
        {
            string? targetDirectory;
            if (args.Length == 1)
            {
                // Taking the path the was provided in cmd line args
                targetDirectory = args[0];
            }
            else if (args.Length > 1)
            {
                Console.WriteLine("ERROR: There can only be one argument, which is the target directory. Please try again by entering the target directory: ");
            }
            else
            {
                Console.WriteLine("Provide the Absolute path of the folder "
                    + "containing \nthe images you want to remove duplicates from: ");
            }

            // Validate user input and prevent attacks (including injection attacks)
            // Repeat the do-while loop and prompt user to try again? (true if the user enters invalid input)
            string? sanitizedPath;
            //string parentDirectory;
            do
            {
                targetDirectory = Console.ReadLine();
                /*
                if (targetDirectory != null)
                {
                    sanitizedPath = IsUserInputValid(targetDirectory);
                } else 
                {
                    sanitizedPath = null;
                }
                */

                sanitizedPath = (targetDirectory != null)? IsUserInputValid(targetDirectory) : null; 

            }
            while (sanitizedPath == null);

            // This variable is not supposed to get reassigned throughout execution
            string parentDirectory = sanitizedPath;

            Console.WriteLine("Processing...");

            string reportDirectory = CreateDirectory("Report", parentDirectory);

            Document? report = CreateReport(reportDirectory);

            // Traverse a directory and its subdirectories. Results is a list of every image's path
#pragma warning disable CS8604 // Possible null reference argument.
            List<Image> imagesToFilter =
            TraverseTargetDirectory(sanitizedPath, parentDirectory, report);
#pragma warning restore CS8604 // Possible null reference argument.

            GroupSimilarImages(sanitizedPath, parentDirectory, imagesToFilter, report);

            Console.WriteLine("Done");
        }

        /// <summary>
        /// Calculate the perceptual hash for an image
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        static string CalculatePerceptualHash(string imagePath)
        {
            using (Bitmap image = new Bitmap(imagePath))
            {
                // Resize the image to a fixed size
                using (Bitmap resizedImage = ResizeImage(image, 32, 32))
                {
                    // Convert the resized image to grayscale
                    using (Bitmap grayscaleImage = ToGrayscale(resizedImage))
                    {
                        // Calculate the average pixel value
                        double averagePixelValue = CalculateAveragePixelValue(grayscaleImage);

                        // Compute the hash
                        string hash = ComputeHash(grayscaleImage, averagePixelValue);
                        return hash;
                    }
                }
            }
        }

        /// <summary>
        /// Resize the image to a fixed size
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        static Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            return new Bitmap(image, new System.Drawing.Size(width, height));
        }

        /// <summary>
        /// Convert the image to grayscale
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        static Bitmap ToGrayscale(Bitmap image)
        {
            Bitmap grayscaleImage = new Bitmap(image.Width, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    System.Drawing.Color color = image.GetPixel(x, y);
                    int average = (color.R + color.G + color.B) / 3;
                    grayscaleImage.SetPixel(x, y, System.Drawing.Color.FromArgb(average, average, average));
                }
            }

            return grayscaleImage;
        }

        /// <summary>
        /// Calculate the average pixel value of the grayscale image
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        static double CalculateAveragePixelValue(Bitmap image)
        {
            double sum = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    sum += image.GetPixel(x, y).R; // Assuming grayscale, so R=G=B
                }
            }

            return sum / (image.Width * image.Height);
        }

        /// <summary>
        /// Compute the hash based on the image's pixel values and average value
        /// </summary>
        /// <param name="image"></param>
        /// <param name="averagePixelValue"></param>
        /// <returns></returns>
        static string ComputeHash(Bitmap image, double averagePixelValue)
        {
            string hash = "";

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    hash += (image.GetPixel(x, y).R > averagePixelValue) ? "1" : "0";
                }
            }

            return hash;
        }

        /// <summary>
        /// Calculate the similarity score between two hashes
        /// </summary>
        /// <param name="hash1">hash of the first image to compare</param>
        /// <param name="hash2">hash of the 2nd image to compare</param>
        /// <returns>Similarity score in percentage</returns>
        /// <exception cref="ArgumentException"></exception>
        static double CalculateSimilarityScore(string hash1, string hash2)
        {
            if (hash1.Length != hash2.Length)
            {
                throw new ArgumentException("Hashes must be of equal length");
            }

            int matchingBits = 0;

            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] == hash2[i])
                {
                    matchingBits++;
                }
            }

            double similarityScore = (double)matchingBits / hash1.Length * 100;

            return similarityScore;
        }

        /// <summary>
        /// This method prevents attacks such as directory traversal attacks 
        /// or injection attacks by converting the user input (the path provided
        /// by the user) to it's canonical path form using Path.GetFullPath(). 
        /// If an error happens in this function, it's likely because the user 
        /// provided an invalid address.</summary>
        /// 
        /// <param name="path">
        /// The path to the folder containing the pictures to 
        /// filer out.</param>
        /// 
        /// <returns>
        /// Returns the sanitized path as a string only if the user enters a 
        /// valid string. If the user's input was invalid (e.g., an empty string
        /// or a path that doesn't exist), a null is returned, which would 
        /// signal the main method to prompt the user for inputting a valid 
        /// path again.</returns>
        static string? IsUserInputValid(string path)
        {
            if (path == null || path.Equals("")) // Ensuring input is not empty
            {
                Console.WriteLine("ERROR: Input cannot have 0 characters. Please try again: ");
                return null;
            }
            else
            {
                try
                {
                    // We'll get the canonical paths as a means of sanitizing the user's input
                    // Source: https://stackoverflow.com/questions/8092314/c-sharp-canonical-file-names
                    string sanitizedPath = Path.GetFullPath(path); // Maybe an error can occurr here, use try-catch with a specific exception class
                    if (!Path.Exists(sanitizedPath)) // If path doesn't exist
                    {
                        Console.WriteLine("ERROR: Path doesn't exist. Try entering a valid path.");
                        return null;
                    }
                    else
                    {
                        Console.WriteLine("The target file which will have the images in "
                        + "it filtered is located at: \n" + sanitizedPath);
                        return sanitizedPath;
                    }
                }
                catch (Exception ex)
                {
                    PrintErrorMessage(ex, "Validating User Input", path, false);
                    return null;
                }
            }
        }

        /// <summary>
        /// Deletes duplicate images
        /// </summary>
        /// <param name="sanitizedPath">Target directory in which we're looking for images</param>
        /// <param name="parentDirectory">A constant, the parent-most target directory</param>
        /// <param name="imagesToFilter">A list of image objects. The images to filter.</param>
        static void GroupSimilarImages(string sanitizedPath, string parentDirectory, List<Image> imagesToFilter, Document report)
        {
            // Going through all the images in a O(N^2) complexity to populate the SimilarImages variable for each image

            // Indicates if an image is similar to another
            const double SimilarityThreshold = 75;
            string txtFileReportPath = $"{parentDirectory}/Report/TxtReport.txt";
            using (StreamWriter writer = new StreamWriter(txtFileReportPath, append: true))
            {
                foreach (Image imageA in imagesToFilter)
                {
                    if (imageA.SimilarToAnotherImage)
                    {
                        // Already falls under another image as similar
                        continue;
                    }
                    else
                    {
                        foreach (Image imageB in imagesToFilter)
                        {
                            if (!imageA.Path.Equals(imageB.Path) && imageB.SimilarToAnotherImage == false)
                            {
                                // If Image A and B are different images and B hasn't been already assigned to another one as a similar image

                                double similarityScore = CalculateSimilarityScore(imageA.PHash, imageB.PHash);
                                
                                writer.WriteLine($"Computing similarity between {Path.GetFileName(imageA.Path)} and {Path.GetFileName(imageB.Path)} = {similarityScore}");

                                if (similarityScore >= SimilarityThreshold && AreDimensionsEqual(imageA.Path, imageB.Path))
                                {
                                    // Similarity detected
                                    imageA.SimilarImages.Add(imageB);
                                    imageB.SimilarToAnotherImage = true;
                                }
                            }
                        }
                    }
                }
            }

            // This will leave the array to only have the parent images along with their similar ones. The total number of parent images + their similar images = the total number of images in all directories and subdirectories.  
            imagesToFilter.RemoveAll(image => image.SimilarToAnotherImage);

            // Write the report here.
            // For every image to filter where its "similarImages" is >= 1, create a dirctory for it.
            int numOfDuplicateImageDirectoriesCreated = 0;

            foreach (Image image in imagesToFilter)
            {
                try
                {
                    if (image.SimilarImages.Count > 0)
                    {
                        string similarImageDirectory = CreateDirectory("Similar Img No. " + ++numOfDuplicateImageDirectoriesCreated,
                        parentDirectory);

                        // Now, dump 'image' along with its similar images into the similarImageDirectory
                        string parentImagePath = image.Path;
                        string destination = Path.Combine(similarImageDirectory, Path.GetFileName(parentImagePath));

                        File.Move(parentImagePath, destination);

                        // Now that the parent image is moved, we're going to move it with its similar images to be in the same file. 
                        foreach (Image similarImage in image.SimilarImages)
                        {
                            string similarImagePath = similarImage.Path;
                            destination = Path.Combine(similarImageDirectory, Path.GetFileName(similarImagePath));

                            File.Move(similarImagePath, destination);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintErrorMessage(ex, "Bucketing Images", image.Path);
                }
            }
            // Move the images here as groups/buckets.
            report.Close();
        }

        static string CreateDirectory(string directoryName, string parentDirectory)
        {
            // The address of the created directory
            string createdDirectory = $"{parentDirectory}/{directoryName}";
            try
            {
                if (!Directory.Exists(createdDirectory))
                {
                    Directory.CreateDirectory(createdDirectory);
                    Console.WriteLine($"{directoryName} directory created!");
                    return createdDirectory;
                }
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex, "Create Directory", createdDirectory);
                return "";
            }
            return createdDirectory;
        }

        static Document? CreateReport(string reportDirectory)
        {
            try
            {
                // Ensure the directory exists
                if (!Directory.Exists(reportDirectory))
                {
                    Console.WriteLine("ERROR: Directory does not exist. Cannot create report.");
                    Environment.Exit(0);
                    return null;
                }

                // Attempt to create the PDF report
                PdfWriter pdfWriter = new PdfWriter($"{reportDirectory}/report.pdf");
                PdfDocument pdfDocument = new PdfDocument(pdfWriter);
                Document report = new Document(pdfDocument);

                return report;
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex, "Report Creation", reportDirectory);
                return null;
            }
        }

        /// <summary>
        /// Explore the directory that was given to us by the user. 
        /// Not only we'll check the directory, but also any other 
        /// subdirectories within the user-provided directory. 
        /// This function will be recursive.
        /// </summary>
        /// <param name="sanitizedPath">The sanitized path to the 
        /// target directory.</param>
        /// <returns>The names of all the pictures found within the 
        /// directory and its sub-directories.</returns>
        static List<Image> TraverseTargetDirectory(string sanitizedPath, string parentDirectory, Document report)
        {
            // We won't traverse the Report and UNSUPPORTED IMAGES files
            if (Path.GetFileName(sanitizedPath).Equals("Report") || Path.GetFileName(sanitizedPath).Equals("Unsupported Images"))
            {
                return [];
            }

            List<Image> imagesFound = new List<Image>();

            // Creating an array of strings where every string is the absolute path of an image in the sanitizedPath directory
            string[] picturePaths = Directory.GetFiles(sanitizedPath);

            foreach (string path in picturePaths)
            {
                // Source: https://docs.sixlabors.com/articles/imagesharp/imageformats.html
                string supportedImageFormatsRegex = @"\.(bmp|gif|jpeg|pbm|png|tiff|tga|webp|jpg)$";

                if (!Regex.IsMatch(GetExtension(path.ToLower()), supportedImageFormatsRegex))
                {
                    string? unsupportedImagesDirectory = CreateDirectory("Unsupported Images", parentDirectory);

                    // Move
                    try
                    {
                        File.Move(path, Path.Combine(unsupportedImagesDirectory, Path.GetFileName(path)));
                        Console.WriteLine("An unsupported image has been moved to the unsupported directory: " + path);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        PrintErrorMessage(ex, "Move Unsupported Image to Unsupported Image file.", Path.Combine(unsupportedImagesDirectory, Path.GetFileName(path)), false);
                    }
                }
                else
                {
                    try
                    {
                        imagesFound.Add(new Image(path, // TODO: Also consider deprecating the path, provided that we can easily obtain it. 
                        CalculatePerceptualHash(path)));
                    }
                    catch (Exception ex)
                    {
                        PrintErrorMessage(ex, "Loading Images", path, false);
                    }
                }
            }

            // Getting the paths of subdirectories and exploring each one
            string[] subdirectories = Directory.GetDirectories(sanitizedPath);
            foreach (string subdirectory in subdirectories)
            {
                imagesFound.AddRange(TraverseTargetDirectory(subdirectory, parentDirectory, report));
            }
            return imagesFound;
        }

        /// <summary>
        /// Given an absolute path of a file, this function returns the extension of that file. 
        /// </summary>
        /// <param name="path">Absolute path</param>
        /// <returns>a string containing the extension of the file.</returns>
        static string GetExtension(string path)
        {
            return Path.GetExtension(path);
        }

        static void PrintErrorMessage(Exception ex, string action, string directory, bool terminateProgram = true)
        {
            if (ex is OutOfMemoryException)
            {
                Console.WriteLine("ERROR: Memory Error.");
            }
            else if (ex is UnauthorizedAccessException)
            {
                Console.WriteLine("ERROR: Unauthorized access. You do not have permission to access files in this location.");
            }
            else if (ex is DirectoryNotFoundException)
            {
                Console.WriteLine("ERROR: Specified directory not found.");
            }
            else if (ex is PathTooLongException)
            {
                Console.WriteLine("ERROR: Path for the location is too long.");
            }
            else if (ex is IOException)
            {
                Console.WriteLine("ERROR: I/O error occurred.");
            }
            else if (ex is ArgumentException)
            {
                Console.WriteLine("ERROR: Invalid argument.");
            }
            else if (ex is FileNotFoundException)
            {
                Console.WriteLine($"ERROR: File wasn't found.");
            }
            else
            {
                Console.WriteLine("ERROR: An unexpected error occurred.");
            }
            Console.WriteLine($"Error message: {ex.Message}\n" +
                    $"Action Type: {action}\n" +
                    $"Directory: {directory}\n");
            if (terminateProgram)
            {
                Environment.Exit(0);
            }
        }

        static bool AreDimensionsEqual(string pathA, string pathB)
        {
            using (System.Drawing.Image metaDataA = System.Drawing.Image.FromFile(pathA))
            {
                using (System.Drawing.Image metaDataB = System.Drawing.Image.FromFile(pathB))
                {
                    int widthA = metaDataA.Width;
                    int heightA = metaDataA.Height;

                    int widthB = metaDataB.Width;
                    int heightB = metaDataB.Height;

                    return (widthA == widthB && heightA == heightB) ? true : false;
                }
            }
        }
    }


    /// <summary>
    /// A class as a primary constructor representing each image in 
    /// the user-provided target directory
    /// </summary>
    /// <param name="path">The absolute path to the image</param>
    /// <param name="pHash">Perceptual hash of this image</param>
    /// <param name="similarToAnotherImage">Prevents an image from having any similar images in its list if it's already similar to another one.</param>
    class Image(string path, string pHash, bool similarToAnotherImage = false)
    {
        public string Path { get; set; } = path;
        public List<Image> SimilarImages { get; set; } = new List<Image>();
        public string PHash { get; set; } = pHash;
        public bool SimilarToAnotherImage { get; set; } = similarToAnotherImage;
    }

}
