using Newtonsoft.Json;
using PIQI.Components.CustomExceptionClasses;
using PIQI.Components.Models;
using PIQI_Engine.Server.Services;
using static PIQI_Engine.Server.Services.FileCacheService;
using CQLTest.Service;

namespace PIQI_Engine.Server.Engines
{
    /// <summary>
    /// Provides functionality to load and manage reference data required by the PIQI engine,
    /// including code systems, SAMs, evaluation rubrics, data types, value lists, models, and entities.
    /// </summary>
    public class ReferenceDataEngine
    {
        /// <summary>
        /// Application configuration instance used for accessing configuration settings.
        /// </summary>
        protected readonly IConfiguration _Configuration;

        /// <summary>
        /// Logger instance for recording informational messages, warnings, and errors related to engine operations.
        /// </summary>
        protected readonly ILogger<PIQIEngine> _Logger;

        /// <summary>
        /// File-based caching service used for storing and retrieving reference data to improve performance.
        /// </summary>
        protected readonly FileCacheService _Cache;

        /// <summary>
        /// Synchronization object used to ensure thread-safe access to the cache.
        /// </summary>
        private static object _CacheLock = new object();

        /// <summary>
        /// Initializes a new instance of <see cref="ReferenceDataEngine"/>.
        /// </summary>
        /// <param name="configuration">The application configuration instance.</param>
        /// <param name="logger">The logger for logging engine operations.</param>
        /// <param name="cache">The file cache service for caching reference data.</param>
        public ReferenceDataEngine(IConfiguration configuration, ILogger<PIQIEngine> logger, FileCacheService cache)
        {
            _Configuration = configuration;
            _Logger = logger; 
            _Cache = cache;
        }

        #region Main
        /// <summary>
        /// Loads all reference data including code systems, SAMs, evaluation rubrics, data types, value lists, value sets, models, entities, and CQL libraries.
        /// This method orchestrates the loading of all required reference data components for PIQI engine operation.
        /// </summary>
        /// <param name="EvaluationRubricMnemonic">The mnemonic identifier of the evaluation rubric to load.</param>
        /// <param name="evaluation">Optional file containing evaluation rubric JSON data. If provided, uses the uploaded rubric instead of cached/configured rubrics.</param>
        /// <returns>A <see cref="PIQIReferenceData"/> object containing all loaded reference data components.</returns>
        /// <exception cref="CustomPIQIException">Thrown when any required reference data component fails to load or is missing.</exception>
        public PIQIReferenceData LoadRefData(string EvaluationRubricMnemonic, IFormFile? evaluation)
        {
            try 
            {
                PIQIReferenceData refData = new PIQIReferenceData();

                // Code systems
                List<CodeSystem>? result1 = LoadCodeSystems(); 
                if (result1 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing or failed to load code systems.");
                refData.CodeSystemList = result1; 

                // Sams
                List<SAM>? result2 = LoadSAMs();
                if (result2 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing or failed to load SAMs.");
                refData.SAMList = result2;

                // evaluation rubric
                EvaluationRubric? result3 = LoadEvaluationRubric(EvaluationRubricMnemonic, evaluation);
                if (result3 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Missing or failed to load evaluation rubric: {EvaluationRubricMnemonic}.");
                refData.EvaluationRubric = result3;

                // Data types
                List<DataType>? result4 = LoadDataTypeList();
                if (result4 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing or failed to load data type list.");
                refData.DataTypeList = result4;
                 
                // Value list
                List<ValueList>? result5 = LoadValueList();
                if (result5 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing or failed to load value list.");  
                refData.ValueList = result5;
                 
                // Value list
                List<ValueSet>? result6 = LoadValueSetList(); 
                if (result6 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing or failed to load value set list.");   
                refData.ValueSetList = result6;

                // Models
                string? evalModelMnemonic = refData.EvaluationRubric?.Model?.Mnemonic;
                if (evalModelMnemonic == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Missing PIQI model mnemonic in evaluation rubric.");

                Model? result8 = LoadModel(evalModelMnemonic);
                if (result8 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Missing or failed to load model: {evalModelMnemonic}."); 
                refData.Model = result8;

                // Entities
                if (refData.Model.DataClasses == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Missing or failed to load entities from the model: {evalModelMnemonic}.");
                refData.EntityModel = new EntityModel(refData.Model);

                // CQL
                // List of all SAM mnemonics associated with the CQL libraries to be loaded
                List<string> CQLSAMMnemonics = ["CQL_EVALUATOR", "CQL_ACIG_EVALUATOR", "CQL_AVI_EVALUATOR", "CQL_PCI_EVALUATOR", "CQL_PSI_EVALUATOR", "CQL_PTI_EVALUATOR", "CQL_AVU_EVALUATOR", "CQL_ACIV_EVALUATOR", "CQL_CO_EVALUATOR", "CQL_CIM_EVALUATOR", "CQL_ACIF_EVALUATOR", "CQL_AVM_EVALUATOR"];
                
                // All CQL library mnemonics from CQL SAMs in the evaluation criteria 
                List<string> cqlMnemonics = refData.EvaluationRubric?.Criteria?
                    .Where(c => CQLSAMMnemonics.Contains(c.SAMMnemonic))
                    .Select(c => c.SAMParameters?.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.SamParameterMnemonic) &&
                        p.SamParameterMnemonic.Equals("CQL_LIBRARY_MNEMONIC"))?.ParameterValue)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .OfType<string>()
                    .ToList() ?? [];
                List<CQLItem>? result9 = LoadCQL(cqlMnemonics);
                if (result9 == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Missing or failed to load CQL: {cqlMnemonics}.");
                refData.CQLList = result9;
                 
                return refData;
            }
            catch 
            {
                throw;
            }
        }
        #endregion

        #region Cache Access
        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> from the cache using the specified cache key.
        /// </summary>
        /// <typeparam name="T">The type of the cached item.</typeparam>
        /// <param name="cacheBaseKey">The key used to identify the cached item.</param>
        /// <returns>
        /// A <see cref="CacheItem{T}"/> containing the requested item if it exists in the cache; 
        /// otherwise, <c>null</c> or an empty item depending on the cache implementation.
        /// </returns>
        protected CacheItem<T>? GetCacheItem<T>(string cacheBaseKey)
        {
            CacheItem<T>? item;
            _Cache.Get<T>(cacheBaseKey, out item);
            return item;
        }

        /// <summary>
        /// Adds or updates an item of type <typeparamref name="T"/> in the cache under the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the item being cached.</typeparam>
        /// <param name="item">The item to add or update in the cache.</param>
        /// <param name="cacheBaseKey">The key under which the item will be stored in the cache.</param>
        /// <remarks>
        /// This method uses a lock on <see cref="_CacheLock"/> to ensure thread-safe cache updates.
        /// </remarks>
        protected void SetCacheItem<T>(T item, string cacheBaseKey)
        {
            lock (_CacheLock)
            {
                _Cache.AddOrUpdate<T>(cacheBaseKey, item);
            }
        }

        /// <summary>
        /// Removes an item in the cache under the specified key.
        /// </summary>
        /// <param name="cacheBaseKey">The key under which the item will be stored in the cache.</param>
        /// <remarks>
        /// This method uses a lock on <see cref="_CacheLock"/> to ensure thread-safe cache updates.
        /// </remarks>
        protected void RemoveCacheItem(string cacheBaseKey)
        {
            lock (_CacheLock)
            {
                _Cache.Remove(cacheBaseKey);
            }
        }

        #endregion

        #region Code Systems
        /// <summary>
        /// Loads the list of supported code systems from cache or configuration file.
        /// Uses file-based caching with automatic invalidation when the source file is modified.
        /// </summary>
        /// <returns>A <see cref="List{CodeSystem}"/> containing all supported code systems, or <c>null</c> if loading fails.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the code systems file is missing or deserialization fails.</exception>
        private List<CodeSystem>? LoadCodeSystems()
        {
            string baseKey = "CODE_SYSTEMS";

            try
            {
                // Get from cache
                CacheItem<List<CodeSystem>>? cacheItem = GetCacheItem<List<CodeSystem>>(baseKey);
                List<CodeSystem>? codeSystems = cacheItem?.Value;

                // Get file path from configuration file
                string? filePath = _Configuration["FilePaths:CodeSystemsPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                    // Check if the code system is cached/if the cache needs to be updated
                    if (codeSystems == null || cacheItem?.LastModified < lastModified)
                    {
                        // Get failed. Load manually.
                        codeSystems = new List<CodeSystem>();

                        // Read file and deserialize the JSON
                        string json = File.ReadAllText(filePath);

                        codeSystems = JsonConvert.DeserializeObject<CodeSystemRoot>(json)?.CodeSystemLibrary;
                        if (codeSystems == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load code systems");

                        // Put code systems in cache
                        SetCacheItem<List<CodeSystem>>(codeSystems, baseKey);
                    }
                }

                return codeSystems;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region Models

        /// <summary>
        /// Loads a specific model by its mnemonic identifier from cache or configuration files.
        /// Checks cache first, then loads from file system if not cached or if the library file has been modified.
        /// </summary>
        /// <param name="modelMnemonic">The unique mnemonic identifier of the model to load.</param>
        /// <returns>A <see cref="Model"/> object containing the requested model definition, or <c>null</c> if not found.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the model library is not found, model file cannot be read, or deserialization fails.</exception>
        public Model? LoadModel(string modelMnemonic)
        {
            string baseKey = "MODEL";
            string libraryKey = "MODEL_LIBRARY";

            try
            {
                // Get from cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                CacheItem<Model>? modelCacheItem = GetCacheItem<Model>($"{baseKey}|{modelMnemonic}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value;
                Model? model = modelCacheItem?.Value;

                // Get file path from configuration file
                string? libraryFilePath = _Configuration["FilePaths:ModelPath"];
                string? modelFilePath = null;
                if (libraryFilePath != null && File.Exists(libraryFilePath))
                {
                    DateTime libraryLastModified = File.GetLastWriteTimeUtc(libraryFilePath);
                    // Create/reset list if needed
                    if (library == null || libraryCacheItem?.LastModified < libraryLastModified)
                        library = new List<ReferenceDataProfile>();

                    // Get the filepath from the library
                    if (library.Any(ep => ep.Mnemonic.Equals(modelMnemonic)))
                        modelFilePath = library.FirstOrDefault(ep => ep.Mnemonic.Equals(modelMnemonic))?.FilePath;
                    else
                    {
                        // Read file and deserialize the JSON to get the list of profiles
                        string profileJson = File.ReadAllText(libraryFilePath);
                        List<ReferenceDataProfile>? modelProfiles = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.ModelProfiles;
                        if (modelProfiles == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load model profiles.");

                        // Add updated library to the cache
                        SetCacheItem<List<ReferenceDataProfile>>(modelProfiles, $"{baseKey}|{libraryKey}");

                        // Get the file path for the model
                        modelFilePath = modelProfiles.FirstOrDefault(e => e.Mnemonic.Equals(modelMnemonic))?.FilePath;
                    }

                    if (modelFilePath == null || !File.Exists(modelFilePath)) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "File for model is invalid or missing.");

                    // Check if the model has been modified since last cached
                    DateTime modelLastModified = File.GetLastWriteTimeUtc(modelFilePath);
                    if (model == null || modelCacheItem?.LastModified < modelLastModified)
                    {
                        // Deserialize the file into an model
                        string rubricJson = File.ReadAllText(modelFilePath);
                        model = JsonConvert.DeserializeObject<Model>(rubricJson);
                        if (model == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load model.");
                    }
                    // Put the model in the cache
                    SetCacheItem<Model>(model, $"{baseKey}|{modelMnemonic}");
                    return model;
                }
                else
                    throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Model library not found.");
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Loads all available models and returns them as a list of <see cref="ReferenceDataDto"/>.
        /// </summary>
        /// <returns>A <see cref="List{ReferenceDataDto}"/> containing all models.</returns>
        public List<ReferenceDataDto> LoadAllModels()
        {
            try
            {
                List<ReferenceDataDto> models = new List<ReferenceDataDto>();

                string? filePath = _Configuration["FilePaths:ModelPath"];
                if (filePath == null || !File.Exists(filePath))
                    throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Invalid or missing model library file.");

                string json = File.ReadAllText(filePath);
                List<ReferenceDataProfile>? baseModels = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(json)?.ModelProfiles;
                if (baseModels == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load models");

                models = baseModels.Select(model => new ReferenceDataDto
                {
                    Name = model.Name,
                    Mnemonic = model.Mnemonic
                }).ToList();

                return models;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Imports a model into the cache. 
        /// </summary>
        /// <param name="modelName">The display name of the model.</param>
        /// <param name="modelMnemonic">A unique identifier (mnemonic) for the model.</param>
        /// <param name="model">The model definition file uploaded as an <see cref="IFormFile"/>.</param>
        /// <remarks>
        /// If a model with the same mnemonic already exists in the cache, 
        /// its definition and metadata are overwritten. Otherwise, a new model profile is added.
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if no file is provided or if deserialization of the model fails.
        /// </exception>
        public void ImportModel(string modelName, string modelMnemonic, IFormFile modelFile)
        {
            string libraryKey = "MODEL_LIBRARY";
            string baseKey = "MODEL";

            try
            {
                // Add model to model cache
                CacheItem<Model>? modelCacheItem = GetCacheItem<Model>($"{baseKey}|{modelMnemonic}");
                Model? modelData = null;

                // Get model
                if (modelFile.Length == 0)
                    throw new CustomPIQIException(400, "INVALID_INPUT", "File not selected");

                using (var reader = new StreamReader(modelFile.OpenReadStream()))
                {
                    string modelJson = reader.ReadToEnd();
                    modelData = JsonConvert.DeserializeObject<Model>(modelJson);
                }
                if (modelData == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load model.");
                SetCacheItem<Model>(modelData, $"{baseKey}|{modelMnemonic}");


                // Add the name and mnemonic to the library cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value ?? new List<ReferenceDataProfile>();

                // Merge with all models from the configuration file
                var allModels = LoadAllModels();
                foreach (var model in allModels)
                {
                    if (!library.Any(l => l.Mnemonic.Equals(model.Mnemonic, StringComparison.OrdinalIgnoreCase)))
                    {
                        library.Add(new ReferenceDataProfile(model.Name, model.Mnemonic));
                    }
                }

                // Find existing item by mnemonic
                var existing = allModels.FirstOrDefault(m =>
                    m.Mnemonic.Equals(modelMnemonic, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    throw new CustomPIQIException(409, "MODEL_CONFLICT", "Mnemonic + Version already exist with different content");
                }
                else
                {
                    // Add new profile
                    library.Add(new ReferenceDataProfile(modelName, modelMnemonic));
                }
                SetCacheItem<List<ReferenceDataProfile>>(library, $"{baseKey}|{libraryKey}");
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Removes a model from the cache.
        /// </summary>
        /// <param name="modelMnemonic">The mnemonic of the model to remove.</param>
        /// <remarks>
        /// The method removes both the model definition and its reference in the 
        /// model library. If no model with the specified mnemonic exists, no action is taken.
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if an error occurs while removing the model from the cache.
        /// </exception>
        public void RemoveModel(string modelMnemonic)
        {
            string libraryKey = "MODEL_LIBRARY";
            string baseKey = "MODEL";

            try
            {
                // Check if the model is in the config file
                string? filePath = _Configuration["FilePaths:ModelPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    string profileJson = File.ReadAllText(filePath);
                    List<ReferenceDataProfile>? modelProfilesFromFile = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.ModelProfiles;
                    if (modelProfilesFromFile != null && modelProfilesFromFile.FirstOrDefault(m => m.Mnemonic == modelMnemonic) != null)
                        throw new CustomPIQIException(405, "NOT_ALLOWED", "Models in the engine configuration file cannot be removed.");
                }

                // Remove the model
                RemoveCacheItem($"{baseKey}|{modelMnemonic}");

                // Remove the model from the library cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value ?? new List<ReferenceDataProfile>();

                // Find existing item by mnemonic
                var existing = library?.FirstOrDefault(l =>
                    l.Mnemonic.Equals(modelMnemonic, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    library.Remove(existing);
                    SetCacheItem<List<ReferenceDataProfile>>(library, $"{baseKey}|{libraryKey}");
                }
                else
                    throw new CustomPIQIException(400, "REFERENCE_DATA_NOT_FOUND", $"Invalid or missing model: {modelMnemonic}");
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Evaluation rubric

        /// <summary>
        /// Loads an evaluation rubric by its mnemonic identifier from either an uploaded file or cached/configured sources.
        /// If an evaluation file is provided, it takes precedence over cached rubrics.
        /// </summary>
        /// <param name="evaluationRubricMnemonic">The unique mnemonic identifier of the evaluation rubric to load.</param>
        /// <param name="evaluation">Optional uploaded evaluation rubric file. If provided, loads from this file instead of cache or configuration.</param>
        /// <returns>An <see cref="EvaluationRubric"/> object with the specified mnemonic, or <c>null</c> if not found.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the evaluation file is invalid, the rubric cannot be found, or deserialization fails.</exception>
        public EvaluationRubric? LoadEvaluationRubric(string evaluationRubricMnemonic, IFormFile? evaluation)
        {
            string baseKey = "EVALUATION_RUBRIC";
            string libraryKey = "EVALUATION_RUBRIC_LIBRARY";

            try
            {
                if (evaluation != null)
                {
                    if (evaluation.Length == 0)
                        throw new CustomPIQIException(400, "INVALID_INPUT", "Evaluation file input not invalid or missing.");

                    using (var reader = new StreamReader(evaluation.OpenReadStream()))
                    {
                        string rubricJson = reader.ReadToEnd();
                        EvaluationRubric? rubric = JsonConvert.DeserializeObject<EvaluationRubric>(rubricJson);
                        if (rubric == null) throw new CustomPIQIException(400, "REFERENCE_DATA_NOT_FOUND", "Failed to load evaluation rubric from evaluation input file.");
                        return rubric;
                    }
                }
                else
                {
                    // Get from cache
                    CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                    CacheItem<EvaluationRubric>? evaluationCacheItem = GetCacheItem<EvaluationRubric>($"{baseKey}|{evaluationRubricMnemonic}");
                    List<ReferenceDataProfile>? library = libraryCacheItem?.Value;
                    EvaluationRubric? evaluationRubric = evaluationCacheItem?.Value;

                    // Get file path from configuration file
                    string? libraryFilePath = _Configuration["FilePaths:EvaluationPath"];
                    string? evaluationFilePath = null;
                    if (libraryFilePath != null && File.Exists(libraryFilePath))
                    {
                        DateTime libraryLastModified = File.GetLastWriteTimeUtc(libraryFilePath);
                        // Create/reset list if needed
                        if (library == null || libraryCacheItem?.LastModified < libraryLastModified)
                            library = new List<ReferenceDataProfile>();

                        // Get the filepath from the library
                        if (library.Any(ep => ep.Mnemonic.Equals(evaluationRubricMnemonic)))
                            evaluationFilePath = library.FirstOrDefault(ep => ep.Mnemonic.Equals(evaluationRubricMnemonic))?.FilePath;
                        else
                        {
                            // Read file and deserialize the JSON to get the list of profiles
                            string profileJson = File.ReadAllText(libraryFilePath);
                            library = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.EvaluationProfiles;
                            if (library == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to resolve evaluation rubric: Failed to load evaluation profiles.");

                            // Add updated library to the cache
                            SetCacheItem<List<ReferenceDataProfile>>(library, $"{baseKey}|{libraryKey}");

                            // Get the file path for the evaluation rubric
                            evaluationFilePath = library.FirstOrDefault(e => e.Mnemonic.Equals(evaluationRubricMnemonic))?.FilePath;
                        }

                        if (evaluationFilePath != null && File.Exists(evaluationFilePath))
                        {
                            // Check if the evaluation rubric has been modified since last cached
                            DateTime evaluationLastModified = File.GetLastWriteTimeUtc(evaluationFilePath);
                            if (evaluationRubric == null || evaluationCacheItem?.LastModified < evaluationLastModified)
                            {
                                // Deserialize the file into an evaluation rubric
                                string rubricJson = File.ReadAllText(evaluationFilePath);
                                evaluationRubric = JsonConvert.DeserializeObject<EvaluationRubric>(rubricJson);
                                if (evaluationRubric == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to resolve evaluation rubric: Failed to load evaluation rubric.");
                            }
                        }
                        else if (evaluationRubric == null) 
                            throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to resolve evaluation rubric: File for evaluation rubric is invalid or missing.");

                        // Update name to match profile name
                        if (library?.FirstOrDefault(ep => ep.Mnemonic.Equals(evaluationRubricMnemonic))?.Name != null)
                            evaluationRubric.Name = library?.FirstOrDefault(ep => ep.Mnemonic.Equals(evaluationRubricMnemonic))?.Name;

                        // Put the evaluation rubric in the cache
                        SetCacheItem<EvaluationRubric>(evaluationRubric, $"{baseKey}|{evaluationRubricMnemonic}");
                        return evaluationRubric;
                    }
                    else
                        throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to resolve evaluation rubric: Evaluation library not found.");
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Loads all evaluation rubrics available in the system.
        /// </summary>
        /// <returns>A <see cref="List{ReferenceDataDto}"/> containing all evaluation rubrics.</returns>
        public List<ReferenceDataDto> LoadAllEvaluationRubrics()
        {
            string baseKey = "EVALUATION_RUBRIC";
            string libraryKey = "EVALUATION_RUBRIC_LIBRARY";

            try
            {
                List<ReferenceDataDto> evaluationProfiles = new List<ReferenceDataDto>();

                // Get evaluations from file
                string? filePath = _Configuration["FilePaths:EvaluationPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    string profileJson = File.ReadAllText(filePath);
                    List<ReferenceDataProfile>? evaluationProfilesFromFile = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.EvaluationProfiles;
                    if (evaluationProfilesFromFile != null)
                    {
                        var profilesFromFile = evaluationProfilesFromFile.Select(e => new ReferenceDataDto
                        {
                            Name = e.Name,
                            Mnemonic = e.Mnemonic
                        }).ToList();
                        evaluationProfiles.AddRange(profilesFromFile);
                    }
                }

                // Get evaluations from cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value;
                if (library != null)
                {
                    var profilesFromCache = library.Select(e => new ReferenceDataDto
                    {
                        Name = e.Name,
                        Mnemonic = e.Mnemonic
                    }).ToList();
                    evaluationProfiles.AddRange(profilesFromCache);

                }

                var distinctEvaluationProfiles = evaluationProfiles
                    .GroupBy(e => e.Mnemonic, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                return distinctEvaluationProfiles;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Imports an evaluation rubric into the cache. 
        /// </summary>
        /// <param name="evaluationName">The display name of the evaluation rubric.</param>
        /// <param name="evaluationMnemonic">A unique identifier (mnemonic) for the evaluation rubric.</param>
        /// <param name="evaluation">The rubric definition file uploaded as an <see cref="IFormFile"/>.</param>
        /// <remarks>
        /// If an evaluation rubric with the same mnemonic already exists in the cache, 
        /// its definition and metadata are overwritten. Otherwise, a new rubric profile is added.
        /// </remarks>
        /// <exception cref="CustomPIQIException">
        /// Thrown if no file is provided or if deserialization of the rubric fails.
        /// </exception>
        public void ImportEvaluation(string evaluationName, string evaluationMnemonic, IFormFile evaluation)
        {
            string libraryKey = "EVALUATION_RUBRIC_LIBRARY";
            string baseKey = "EVALUATION_RUBRIC";

            try
            {
                // Add evaluation to evaluation cache
                CacheItem<EvaluationRubric>? evaluationCacheItem = GetCacheItem<EvaluationRubric>($"{baseKey}|{evaluationMnemonic}");
                EvaluationRubric? evaluationRubric = null;

                // Get evaluation
                if (evaluation.Length == 0)
                    throw new CustomPIQIException(400, "INVALID_INPUT", "File not selected");

                using (var reader = new StreamReader(evaluation.OpenReadStream()))
                {
                    string rubricJson = reader.ReadToEnd();
                    evaluationRubric = JsonConvert.DeserializeObject<EvaluationRubric>(rubricJson);
                }
                if (evaluationRubric == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load evaluation rubric.");
                SetCacheItem<EvaluationRubric>(evaluationRubric, $"{baseKey}|{evaluationMnemonic}");


                // Add the name and mnemonic to the library cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value ?? new List<ReferenceDataProfile>();

                // Merge with all evaluation rubrics from the configuration file
                var allEvaluations = LoadAllEvaluationRubrics();
                foreach (var eval in allEvaluations)
                {
                    if (!library.Any(l => l.Mnemonic.Equals(eval.Mnemonic, StringComparison.OrdinalIgnoreCase)))
                    {
                        library.Add(new ReferenceDataProfile(eval.Name, eval.Mnemonic));
                    }
                }

                // Find existing item by mnemonic
                var existing = allEvaluations.FirstOrDefault(e =>
                    e.Mnemonic.Equals(evaluationMnemonic, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    throw new CustomPIQIException(409, "EVALUATION_CONFLICT", "Mnemonic + Version already exist with different content");
                }
                else
                {
                    // Add new profile
                    library.Add(new ReferenceDataProfile(evaluationName, evaluationMnemonic));
                }
                SetCacheItem<List<ReferenceDataProfile>>(library, $"{baseKey}|{libraryKey}");
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Removes an evaluation rubric from the cache.
        /// </summary>
        /// <param name="evaluationMnemonic">The mnemonic of the evaluation rubric to remove.</param>
        /// <remarks>
        /// The method removes both the rubric definition and its reference in the 
        /// rubric library. If no rubric with the specified mnemonic exists, no action is taken.
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if an error occurs while removing the rubric from the cache.
        /// </exception>
        public void RemoveEvaluation(string evaluationMnemonic)
        {
            string libraryKey = "EVALUATION_RUBRIC_LIBRARY";
            string baseKey = "EVALUATION_RUBRIC";

            try
            {
                // Check if the evaluation is in the config file
                string? filePath = _Configuration["FilePaths:EvaluationPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    string profileJson = File.ReadAllText(filePath);
                    List<ReferenceDataProfile>? evaluationProfilesFromFile = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.EvaluationProfiles;
                    if (evaluationProfilesFromFile != null && evaluationProfilesFromFile.FirstOrDefault(e => e.Mnemonic == evaluationMnemonic) != null)
                        throw new CustomPIQIException(405, "NOT_ALLOWED", "Evaluation rubrics in the engine configuration file cannot be removed.");
                }

                // Remove the evaluation
                RemoveCacheItem($"{baseKey}|{evaluationMnemonic}");

                // Remove the evaluation from the library cache
                CacheItem<List<ReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<ReferenceDataProfile>>($"{baseKey}|{libraryKey}");
                List<ReferenceDataProfile>? library = libraryCacheItem?.Value ?? new List<ReferenceDataProfile>();

                // Find existing item by mnemonic
                var existing = library?.FirstOrDefault(l =>
                    l.Mnemonic.Equals(evaluationMnemonic, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    library.Remove(existing);
                    SetCacheItem<List<ReferenceDataProfile>>(library, $"{baseKey}|{libraryKey}");
                }
                else
                    throw new CustomPIQIException(400, "INVALID_INPUT", $"Invalid or missing evaluation: {evaluationMnemonic}");
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region SAMs

        /// <summary>
        /// Loads the complete list of all supported Semantic Assessment Modules (SAMs) from cache or configuration file.
        /// Uses file-based caching with automatic invalidation when the source file is modified.
        /// </summary>
        /// <returns>A <see cref="List{SAM}"/> containing all supported SAMs, or <c>null</c> if loading fails.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the SAMs file is missing or deserialization fails.</exception>
        public List<SAM>? LoadSAMs()
        {
            string baseKey = "SAMS";

            try
            {
                // Get from cache
                CacheItem<List<SAM>>? cacheItem = GetCacheItem<List<SAM>>(baseKey);
                List<SAM>? sams = cacheItem?.Value;

                // Get file path from configuration file
                string? filePath = _Configuration["FilePaths:SAMsPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                    // Check if the code system is cached/if the cache needs to be updated
                    if (sams == null || cacheItem?.LastModified < lastModified)
                    {
                        // Get failed. Load manually.
                        sams = new List<SAM>();

                        // Read file and deserialize the JSON
                        string json = File.ReadAllText(filePath);

                        sams = JsonConvert.DeserializeObject<SAMRoot>(json)?.SAMLibrary;
                        if (sams == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load SAMs");

                        // Put SAMs in cache
                        SetCacheItem<List<SAM>>(sams, baseKey);
                    }
                }

                return sams;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Loads a single Semantic Assessment Module (SAM) by its mnemonic identifier.
        /// Searches the cached SAM library first, then reloads from file if not found or cache is stale.
        /// </summary>
        /// <param name="mnemonic">The unique mnemonic identifier of the SAM to load.</param>
        /// <returns>A <see cref="SAM"/> object with the specified mnemonic.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the SAM library is not found, the specified SAM doesn't exist, or deserialization fails.</exception>
        public SAM? LoadSAM(string mnemonic)
        {
            string baseKey = "SAMs";

            try
            {
                // Get from cache
                CacheItem<List<SAM>>? libraryCacheItem = GetCacheItem<List<SAM>>(baseKey);
                List<SAM>? library = libraryCacheItem?.Value; 

                // Get file path from configuration file
                string? libraryFilePath = _Configuration["FilePaths:SAMsPath"];
                if (libraryFilePath != null && File.Exists(libraryFilePath))
                {
                    DateTime libraryLastModified = File.GetLastWriteTimeUtc(libraryFilePath);
                    // Create/reset list if needed
                    if (library == null || libraryCacheItem?.LastModified < libraryLastModified)
                        library = new List<SAM>();

                    // Check if the SAM is in the cache
                    SAM? sam = library.FirstOrDefault(s => s.Mnemonic.Equals(mnemonic));
                    if (sam != null) return sam;
                    else
                    {
                        // Read file and deserialize the JSON to get the list of profiles
                        string samLibraryJson = File.ReadAllText(libraryFilePath);
                        library = JsonConvert.DeserializeObject<SAMRoot>(samLibraryJson)?.SAMLibrary;
                        if (library == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to SAM library.");

                        // Add updated library to the cache
                        SetCacheItem<List<SAM>>(library, baseKey);

                        // Get the SAM with the matching mnemonic 
                        sam = library.FirstOrDefault(s => s.Mnemonic.Equals(mnemonic));
                        if (sam != null) return sam;
                        else throw new CustomPIQIException(400, "INVALID_INPUT", $"Missing or invalid SAM: [{mnemonic}].");
                    }
                } 
                else
                    throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "SAM library not found.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion  

        #region CQL Libraries
        /// <summary>
        /// Loads multiple Clinical Quality Language (CQL) library items by their mnemonic identifiers.
        /// Retrieves CQL library definitions and field mappings from cache or configuration files.
        /// </summary>
        /// <param name="cqlMnemonics">List of mnemonic identifiers for the CQL libraries to load.</param>
        /// <returns>A <see cref="List{CQLItem}"/> containing the requested CQL library items with their text and field mappings.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the CQL library file is missing, a requested CQL item is not found, or deserialization fails.</exception>
        public List<CQLItem>? LoadCQL(List<string> cqlMnemonics)
        {
            string baseKey = "CQL";
            string cqlLibraryKey = "CQL_LIBRARY";
            List<CQLItem>? cqlItemList = new List<CQLItem>();

            try
            {
                // Try to get from cache
                CacheItem<List<CQLReferenceDataProfile>>? libraryCacheItem = GetCacheItem<List<CQLReferenceDataProfile>>($"{baseKey}|{cqlLibraryKey}");
                List<CQLReferenceDataProfile>? cqlLibrary = libraryCacheItem?.Value;

                // Get file path from configuration file 
                string? cqlLibraryFilePath = _Configuration["FilePaths:CQLPath"];
                string? cqlFilePath = null;
                if (cqlLibraryFilePath != null && File.Exists(cqlLibraryFilePath))
                {
                    DateTime libraryLastModified = File.GetLastWriteTimeUtc(cqlLibraryFilePath);
                    // Create/reset list if needed
                    if (cqlLibrary == null || libraryCacheItem?.LastModified < libraryLastModified)
                    {
                        // Read profile file and deserialize the JSON to get the list of profiles
                        string profileJson = File.ReadAllText(cqlLibraryFilePath);
                        cqlLibrary = JsonConvert.DeserializeObject<ReferenceDataProfileRoot>(profileJson)?.CQLProfiles;
                        if (cqlLibrary == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Failed to load CQL library.");

                        // Add updated library to the cache
                        SetCacheItem<List<CQLReferenceDataProfile>>(cqlLibrary, $"{baseKey}|{cqlLibraryKey}");
                    }

                    foreach (string cqlItemMnemonic in cqlMnemonics)
                    {
                        // Try to get from cache
                        CacheItem<CQLItem>? cqlCacheItem = GetCacheItem<CQLItem>($"{baseKey}|{cqlItemMnemonic}");
                        CQLItem? cqlItem = cqlCacheItem?.Value;

                        // Get the filepath from the library
                        CQLReferenceDataProfile? cqlProfile = cqlLibrary.FirstOrDefault(ep => ep.Mnemonic.Equals(cqlItemMnemonic));
                        cqlFilePath = cqlProfile?.FilePath;
                        if (cqlProfile == null || cqlFilePath == null || !File.Exists(cqlFilePath)) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"File for CQL is invalid or missing: {cqlItemMnemonic}.");

                        // Check if the CQL has been modified since last cached
                        DateTime cqlLastModified = File.GetLastWriteTimeUtc(cqlFilePath);
                        if (cqlItem == null || cqlCacheItem?.LastModified < cqlLastModified)
                        {
                            // Deserialize the file into a CQL
                            string cql = File.ReadAllText(cqlFilePath);
                            FieldMappings? fieldMappings = null;
                            string? fieldMappingFilePath = cqlProfile?.FieldMappingFilePath;
                            if (fieldMappingFilePath != null && File.Exists(fieldMappingFilePath))
                            {
                                string fieldMappingJson = File.ReadAllText(fieldMappingFilePath);
                                fieldMappings = JsonConvert.DeserializeObject<FieldMappings>(fieldMappingJson);
                            }
                            cqlItem = new CQLItem(cqlProfile, cql, fieldMappings);
                            if (cqlItem == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", $"Failed to load CQL: {cqlItemMnemonic}.");
                        }

                        // Put the CQL item in the cache and add it to the return list
                        SetCacheItem<CQLItem>(cqlItem, $"{baseKey}|{cqlItemMnemonic}");
                        cqlItemList.Add(cqlItem);
                    }
                }
                else
                    throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "CQL library not found.");

                return cqlItemList;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region Data Types

        /// <summary>
        /// Loads the list of all supported data types from cache or configuration file.
        /// Uses file-based caching with automatic invalidation when the source file is modified.
        /// </summary>
        /// <returns>A <see cref="List{DataType}"/> containing all supported data types, or <c>null</c> if loading fails.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the data types file is missing or deserialization fails.</exception>
        private List<DataType>? LoadDataTypeList()
        {
            string baseKey = "DATA_TYPE_LIST";

            try
            {
                // Get from cache
                CacheItem<List<DataType>>? cacheItem = GetCacheItem<List<DataType>>(baseKey);
                List<DataType>? dataTypes = cacheItem?.Value;

                // Get file path from configuration file
                string? filePath = _Configuration["FilePaths:DataTypesPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                    // Check if the code system is cached/if the cache needs to be updated
                    if (dataTypes == null || cacheItem?.LastModified < lastModified)
                    {
                        // Get failed. Load manually from file
                        dataTypes = new List<DataType>();

                        // Read file and deserialize the JSON
                        string json = File.ReadAllText(filePath);

                        dataTypes = JsonConvert.DeserializeObject<DataTypeRoot>(json)?.DataTypeLibrary;
                        if (dataTypes == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load data types");

                        // Put in cache
                        SetCacheItem<List<DataType>>(dataTypes, baseKey);
                    }
                }

                return dataTypes;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region ValueList
        /// <summary>
        /// Loads the list of all supported value lists from cache or configuration file.
        /// Value lists define controlled vocabularies used for validation and data entry.
        /// Uses file-based caching with automatic invalidation when the source file is modified.
        /// </summary>
        /// <returns>A <see cref="List{ValueList}"/> containing all supported value lists, or <c>null</c> if loading fails.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the value list file is missing or deserialization fails.</exception>
        private List<ValueList>? LoadValueList()
        {
            string baseKey = "VALUE_LIST";

            try
            {
                // Get from cache
                CacheItem<List<ValueList>>? cacheItem = GetCacheItem<List<ValueList>>(baseKey);
                List<ValueList>? valueList = cacheItem?.Value;

                // Get file path from configuration file
                string? filePath = _Configuration["FilePaths:ValueListPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                    // Check if the code system is cached/if the cache needs to be updated
                    if (valueList == null || cacheItem?.LastModified < lastModified)
                    {
                        // Get failed. Load manually.
                        valueList = new List<ValueList>();

                        // Read file and deserialize the JSON
                        string json = File.ReadAllText(filePath);

                        valueList = JsonConvert.DeserializeObject<ValueListRoot>(json)?.ValueLibrary;
                        if (valueList == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load value list");

                        // Put value list in cache
                        SetCacheItem<List<ValueList>>(valueList, baseKey);
                    }
                }

                return valueList;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region ValueSetList
        /// <summary>
        /// Loads the list of all supported value sets from cache or configuration file.
        /// Value sets are collections of coded concepts used for standardized terminology.
        /// Uses file-based caching with automatic invalidation when the source file is modified.
        /// </summary>
        /// <returns>A <see cref="List{ValueSet}"/> containing all supported value sets, or <c>null</c> if loading fails.</returns>
        /// <exception cref="CustomPIQIException">Thrown when the value set list file is missing or deserialization fails.</exception>
        private List<ValueSet>? LoadValueSetList()
        {
            string baseKey = "VALUE_SET_LIST";

            try
            {
                // Get from cache
                CacheItem<List<ValueSet>>? cacheItem = GetCacheItem<List<ValueSet>>(baseKey);
                List<ValueSet>? valueList = cacheItem?.Value;

                // Get file path from configuration file
                string? filePath = _Configuration["FilePaths:ValueSetListPath"];
                if (filePath != null && File.Exists(filePath))
                {
                    DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                    // Check if the code system is cached/if the cache needs to be updated
                    if (valueList == null || cacheItem?.LastModified < lastModified)
                    {
                        // Get failed. Load manually.
                        valueList = new List<ValueSet>();

                        // Read file and deserialize the JSON
                        string json = File.ReadAllText(filePath);

                        valueList = JsonConvert.DeserializeObject<ValueSetListRoot>(json)?.ValueSetLibrary;
                        if (valueList == null) throw new CustomPIQIException(422, "REFERENCE_DATA_NOT_FOUND", "Failed to load value set list");

                        // Put value list in cache
                        SetCacheItem<List<ValueSet>>(valueList, baseKey);
                    }
                }

                return valueList;
            }
            catch
            {
                throw;
            }
        }
        #endregion

    }
}
