using Python.Runtime;
using Microsoft.Extensions.Logging;

public class RecommendationService
{
    private readonly ILogger<RecommendationService> _logger;
    private dynamic? _model;
    private int[]? _allProductIds;
    private Dictionary<int, int>? _customerMap;
    private Dictionary<int, int>? _productMap;
    private readonly object _lock = new();
    private bool _isInitialized = false;

    public RecommendationService(ILogger<RecommendationService> logger)
    {
        _logger = logger;
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (!_isInitialized)
            {
                Runtime.PythonDLL = @"C:\Users\GIGABYTE\AppData\Local\Programs\Python\Python39\python39.dll";
                PythonEngine.Initialize();
                LoadModel();
                _isInitialized = true;
            }
        }
    }

    public void LoadModel()
    {
        try
        {
            var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "trainmodel", "lightfm_model.pkl");
            if (!File.Exists(modelPath)) return;

            var bytes = File.ReadAllBytes(modelPath);
            using (Py.GIL())
            {
                dynamic pickle = Py.Import("pickle");
                dynamic loaded = pickle.loads(bytes);
                (_model as PyObject)?.Dispose();
                _model = loaded["model"];
                dynamic dataset = loaded["dataset"];
                var mapping = dataset.mapping();
                _customerMap = mapping[0].As<Dictionary<int, int>>();
                _productMap = mapping[2].As<Dictionary<int, int>>();
                _allProductIds = _productMap.Keys.OrderBy(x => x).ToArray();
            }
            _logger.LogInformation("Python model loaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model");
        }
    }
    public void ReloadModel()
    {
        lock (_lock)
        {
            if (!_isInitialized)
            {
                Runtime.PythonDLL = @"C:\Users\GIGABYTE\AppData\Local\Programs\Python\Python39\python39.dll";
                PythonEngine.Initialize();
                _isInitialized = true;
            }

            LoadModel();
        }
    }
    public async Task<List<int>> PredictTopKAsync(int userId, int topK)
    {
        EnsureInitialized();

        if (_model == null || _customerMap == null || _allProductIds == null)
            return new List<int>();

        if (!_customerMap.TryGetValue(userId, out int internalId))
            return new List<int>();

        return await Task.Run(() =>
        {
            using (Py.GIL())
            {
                lock (_lock)
                {
                    var scores = _model.predict(internalId, _allProductIds);
                    var result = new List<(float, int)>();
                    for (int i = 0; i < scores.shape[0]; i++)
                        result.Add((scores[i].As<float>(), _allProductIds[i]));
                    return result.OrderByDescending(x => x.Item1).Take(topK).Select(x => x.Item2).ToList();
                }
            }
        });
    }
}