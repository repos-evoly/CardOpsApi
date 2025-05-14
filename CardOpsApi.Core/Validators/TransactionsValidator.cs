using System;
using CardOpsApi.Core.Dtos;
using FluentValidation;

namespace CardOpsApi.Core.Validators
{
    // Validator for creating a new Transaction record.
    public class TransactionCreateDtoValidator : AbstractValidator<TransactionCreateDto>
    {
        public TransactionCreateDtoValidator()
        {
            RuleFor(x => x.FromAccount)
                .NotEmpty().WithMessage("FromAccount is required.")
                .Length(13).WithMessage("FromAccount Should be 13 digits");

            RuleFor(x => x.Narrative)
                .MaximumLength(250).WithMessage("Narrative cannot exceed 250 characters.");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Type is required.")
                .Must(type => type.Equals("ATM", StringComparison.OrdinalIgnoreCase) ||
                              type.Equals("POS", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Type must be either ATM or POS.");

            RuleFor(x => x.DefinitionId)
                .GreaterThan(0).WithMessage("DefinitionId must be greater than 0.");

            // Optional: Ensure Amount is non-negative if provided.
            RuleFor(x => x.Amount)
                .GreaterThanOrEqualTo(0).WithMessage("Amount cannot be negative.")
                .WithMessage("Amount cannot be negative.");
        }
    }

    // Validator for updating an existing Transaction record.
    public class TransactionUpdateDtoValidator : AbstractValidator<TransactionUpdateDto>
    {
        public TransactionUpdateDtoValidator()
        {
            RuleFor(x => x.FromAccount)
                .NotEmpty().WithMessage("FromAccount is required.")
                .MaximumLength(50).WithMessage("FromAccount cannot exceed 50 characters.");

            RuleFor(x => x.Narrative)
                .MaximumLength(250).WithMessage("Narrative cannot exceed 250 characters.");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Type is required.")
                .Must(type => type.Equals("ATM", StringComparison.OrdinalIgnoreCase) ||
                              type.Equals("POS", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Type must be either ATM or POS.");

            RuleFor(x => x.DefinitionId)
                .GreaterThan(0).WithMessage("DefinitionId must be greater than 0.");

            // Optional: Ensure Amount is non-negative if provided.
            RuleFor(x => x.Amount)
                .GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue)
                .WithMessage("Amount cannot be negative.");

            // Date should be a valid date/time; if you want additional checks (e.g., not in the future), add them here.
        }
    }
}
