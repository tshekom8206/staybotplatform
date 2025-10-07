using Microsoft.EntityFrameworkCore;
using Quartz;
using Hostr.Workers.Data;
using Hostr.Workers.Services;
using Pgvector;

namespace Hostr.Workers.Services;

public interface IEmbeddingsService
{
    Task<float[]?> GenerateEmbeddingAsync(string text);
}

public class EmbeddingsService : IEmbeddingsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmbeddingsService> _logger;

    public EmbeddingsService(IConfiguration configuration, ILogger<EmbeddingsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        try
        {
            // Simplified OpenAI embedding call
            // In production, use proper OpenAI client
            await Task.Delay(100); // Simulate API call
            
            // Return dummy embedding for now
            var random = new Random();
            return Enumerable.Range(0, 1536).Select(_ => (float)random.NextDouble()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return null;
        }
    }
}

[DisallowConcurrentExecution]
public class EmbeddingsWorker : IJob
{
    private readonly WorkersDbContext _context;
    private readonly IEmbeddingsService _embeddingsService;
    private readonly ILogger<EmbeddingsWorker> _logger;

    public EmbeddingsWorker(
        WorkersDbContext context, 
        IEmbeddingsService embeddingsService,
        ILogger<EmbeddingsWorker> logger)
    {
        _context = context;
        _embeddingsService = embeddingsService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting embeddings worker");

        try
        {
            // Process FAQs that need embeddings
            var faqsToEmbed = await _context.FAQs
                .Where(f => f.NeedsEmbedding)
                .Take(10) // Process in batches
                .ToListAsync();

            foreach (var faq in faqsToEmbed)
            {
                var embedding = await _embeddingsService.GenerateEmbeddingAsync(faq.Question);
                if (embedding != null)
                {
                    // Update the knowledge base chunk or create one
                    var existingChunk = await _context.KnowledgeBaseChunks
                        .FirstOrDefaultAsync(k => k.Source == $"FAQ-{faq.Id}");

                    if (existingChunk != null)
                    {
                        existingChunk.Content = faq.Question;
                        existingChunk.Embedding = new Vector(embedding);
                        existingChunk.UpdatedAt = DateTime.UtcNow;
                        existingChunk.NeedsEmbedding = false;
                    }
                    else
                    {
                        var newChunk = new Models.KnowledgeBaseChunk
                        {
                            TenantId = faq.TenantId,
                            Source = $"FAQ-{faq.Id}",
                            Language = faq.Language,
                            Content = faq.Question,
                            Embedding = new Vector(embedding),
                            UpdatedAt = DateTime.UtcNow,
                            NeedsEmbedding = false
                        };
                        
                        _context.KnowledgeBaseChunks.Add(newChunk);
                    }

                    faq.NeedsEmbedding = false;
                }
            }

            // Process knowledge base chunks that need embeddings
            var chunksToEmbed = await _context.KnowledgeBaseChunks
                .Where(k => k.NeedsEmbedding)
                .Take(10)
                .ToListAsync();

            foreach (var chunk in chunksToEmbed)
            {
                var embedding = await _embeddingsService.GenerateEmbeddingAsync(chunk.Content);
                if (embedding != null)
                {
                    chunk.Embedding = new Vector(embedding);
                    chunk.NeedsEmbedding = false;
                    chunk.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Processed {FaqCount} FAQs and {ChunkCount} chunks", 
                faqsToEmbed.Count, chunksToEmbed.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in embeddings worker");
        }
    }
}