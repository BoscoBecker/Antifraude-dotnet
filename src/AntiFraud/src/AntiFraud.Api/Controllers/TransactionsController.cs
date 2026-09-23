using AntiFraud.Application.Contracts;
using AntiFraud.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace AntiFraud.Api.Controllers;

[ApiController]
[Route("transactions")]
public sealed class TransactionsController : ControllerBase
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Recebe uma transação para avaliação antifraude. Requer header Idempotency-Key.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Idempotency-Key",
                Detail = $"Header '{IdempotencyHeader}' is required (8–128 characters)."
            });
        }

        try
        {
            var (response, created) = await _transactionService.SubmitAsync(
                idempotencyKey.ToString(),
                request,
                cancellationToken);

            if (created)
            {
                return Accepted($"/transactions/{response.Id}", response);
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _transactionService.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
