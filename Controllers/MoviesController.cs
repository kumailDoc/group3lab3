using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MovieStreamingApp.Models;
using MovieStreamingApp.Services;
using Microsoft.Extensions.Logging;

namespace MovieStreamingApp.Controllers
{
    public class MoviesController : Controller
    {
        private readonly S3Service _s3Service;
        private readonly DynamoDbService _dynamoDbService;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(S3Service s3Service, DynamoDbService dynamoDbService, ILogger<MoviesController> logger)
        {
            _s3Service = s3Service;
            _dynamoDbService = dynamoDbService;
            _logger = logger;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Index method invoked: Fetching all movies from DynamoDB.");

            var movieDocuments = await _dynamoDbService.GetAllMoviesAsync();

            var movies = new List<Movie>();
            foreach (var doc in movieDocuments)
            {
                var movie = new Movie
                {
                    MovieID = doc["MovieID"],
                    Title = doc["Title"],
                    Genre = doc["Genre"],
                    Director = doc["Director"],
                    ReleaseTime = doc.ContainsKey("ReleaseTime") ? doc["ReleaseTime"] : "", // Handle missing ReleaseTime field
                    Rating = doc.ContainsKey("Rating") ? doc["Rating"].AsInt() : 0, // Handle missing Rating field
                    FileUrl = doc.ContainsKey("FileUrl") ? doc["FileUrl"] : "", // Handle missing FileUrl field
                    Comments = doc.ContainsKey("Comments") ? doc["Comments"] : "", // Handle missing Comments field
                    OwnerId = doc.ContainsKey("OwnerId") ? doc["OwnerId"].AsInt() : 0 // Handle missing OwnerId field
                };
                movies.Add(movie);
            }

            return View(movies);
        }

        // GET: Movies/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Create GET method invoked: Displaying the create movie form.");
            return View();
        }

        // POST: Movies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Movie movie, IFormFile? movieFile)
        {
            _logger.LogInformation("Create POST method invoked.");

            // Set MovieID before validation
            movie.MovieID = string.IsNullOrEmpty(movie.MovieID) ? Guid.NewGuid().ToString() : movie.MovieID;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for Create method. Errors: {Errors}",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());
                return View(movie);
            }

            try
            {
                // Set the OwnerId to the logged-in user's ID
                movie.OwnerId = HttpContext.Session.GetInt32("UserId").GetValueOrDefault();
                movie.Comments ??= ""; // Ensure Comments is initialized

                if (movieFile != null && movieFile.Length > 0)
                {
                    _logger.LogInformation("Uploading file to S3: {FileName}", movieFile.FileName);
                    using var stream = movieFile.OpenReadStream();
                    movie.FileUrl = await _s3Service.UploadFileAsync(stream, movieFile.FileName);
                    _logger.LogInformation("File uploaded successfully. File URL: {FileUrl}", movie.FileUrl);
                }
                else
                {
                    movie.FileUrl ??= "No file uploaded"; // Set a default if no file is provided
                }

                // Save metadata to DynamoDB with ownerId parameter included
                await _dynamoDbService.SaveMovieAsync(
                    movie.MovieID,
                    movie.Title,
                    movie.Genre,
                    movie.Director,
                    movie.ReleaseTime,
                    movie.Rating,
                    movie.FileUrl,
                    movie.Comments,
                    movie.OwnerId
                );

                _logger.LogInformation("Movie metadata saved successfully to DynamoDB for MovieID: {MovieID}", movie.MovieID);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a movie. MovieID: {MovieID}", movie.MovieID);
                return View(movie);
            }
        }

    }
}
