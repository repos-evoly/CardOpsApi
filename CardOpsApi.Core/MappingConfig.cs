using AutoMapper;
using CardOpsApi.Data.Models;
using CardOpsApi.Core.Dtos;

namespace CardOpsApi
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {

            CreateMap<DefinitionCreateDto, Definition>();
            CreateMap<DefinitionUpdateDto, Definition>();
            CreateMap<Definition, DefinitionDto>();

            // Transactions
            CreateMap<TransactionCreateDto, Transactions>();

            CreateMap<TransactionUpdateDto, Transactions>()
             // Always take the ReasonId from the DTO.
             .ForMember(dest => dest.ReasonId, opt => opt.MapFrom(src => src.ReasonId));

            CreateMap<Transactions, TransactionDto>()
                .ForMember(dest => dest.CurrencyCode, opt => opt.MapFrom(src => src.Currency.Code))
                .ForMember(dest => dest.ReasonName, opt => opt.MapFrom(src => src.Reason != null ? src.Reason.NameAR : string.Empty)); ;


            CreateMap<Currency, CurrencyDto>();
            CreateMap<CurrencyCreateDto, Currency>();
            CreateMap<CurrencyUpdateDto, Currency>();

            CreateMap<Reason, ReasonDto>();
            CreateMap<ReasonCreateDto, Reason>();
            CreateMap<ReasonUpdateDto, Reason>();


            CreateMap<Settings, SettingsDto>()
              .ForMember(dest => dest.TopAtmRefundLimit, opt => opt.MapFrom(src => src.TopAtmRefundLimit))
              .ForMember(dest => dest.TopReasonLimit, opt => opt.MapFrom(src => src.TopReasonLimit))
              .ReverseMap();
        }
    }

}
