using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Application.Errors;
using Application.Interfaces;
using Domain;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Trails
{
    public class Create
    {
        public class Query : IRequest<Trail>
        {
            public Trail Trail { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(x => x.Trail.Name).NotEmpty();
                RuleFor(x => x.Trail.Description).NotEmpty();
                RuleFor(x => x.Trail.Length).NotEmpty();
                RuleFor(x => x.Trail.Difficulty).NotEmpty();
                RuleFor(x => x.Trail.Type).NotEmpty();
                RuleFor(x => x.Trail.Traffic).NotEmpty();
                RuleFor(x => x.Trail.Attractions).NotEmpty();
                RuleFor(x => x.Trail.Suitabilities).NotEmpty();
                RuleFor(x => x.Trail.Trailhead.Country).NotEmpty();
                RuleFor(x => x.Trail.Trailhead.Street).NotEmpty().When(x => x.Trail.Trailhead.Country == "United States");
                RuleFor(x => x.Trail.Trailhead.City).NotEmpty().When(x => x.Trail.Trailhead.Country == "United States");
                RuleFor(x => x.Trail.Trailhead.State).NotEmpty().When(x => x.Trail.Trailhead.Country == "United States");
                RuleFor(x => x.Trail.Trailhead.County).NotEmpty().When(x => x.Trail.Trailhead.Country == "United States");
                RuleFor(x => x.Trail.Trailhead.PostalCode).NotEmpty().When(x => x.Trail.Trailhead.Country == "United States");
                RuleFor(x => x.Trail.Trailhead.Latitude).NotEmpty();
                RuleFor(x => x.Trail.Trailhead.Longitude).NotEmpty();
                RuleFor(x => x.Trail.Trailhead.InternationalLocation.Address).NotEmpty().When(x => x.Trail.Trailhead.Country != "United States");
            }
        }

        public class Handler : IRequestHandler<Query, Trail>
        {
            private readonly UserManager<User> userManager;
            private readonly IUserAccessor userAccessor;
            private readonly ITrailsService trailsService;
            public Handler(UserManager<User> userManager, IUserAccessor userAccessor, ITrailsService trailsService)
            {
                this.userManager = userManager;
                this.userAccessor = userAccessor;
                this.trailsService = trailsService;
            }

            public async Task<Trail> Handle(Query request, CancellationToken cancellationToken)
            {
                var currentUserId = userAccessor.GetCurrentUserId();

                var currentUser = await userManager.FindByIdAsync(currentUserId);
                if (currentUser == null) throw new RestException(HttpStatusCode.BadRequest, new { User = "User not found. Permission Denied." });
                if (await userManager.IsInRoleAsync(currentUser, RoleNames.User))
                {
                    throw new RestException(HttpStatusCode.Forbidden, new { Forbidden = "Insufficient role privileges. Permission Denied." });
                }

                var trailNameExists = trailsService.CheckExistsByName(request.Trail.Name);
                if (trailNameExists) throw new RestException(HttpStatusCode.BadRequest, new { Trail = "Name already exists." });

                var trailAddressExists = trailsService.CheckExistsByAddress(request.Trail.Trailhead.Address);
                if (trailAddressExists) throw new RestException(HttpStatusCode.BadRequest, new { Trail = "Trailhead location already exists." });
                //TODO: add same check for location coordinates

                var newTrail = new Trail
                {
                    Name = request.Trail.Name,
                    Description = request.Trail.Description,
                    Length = request.Trail.Length,
                    Difficulty = request.Trail.Difficulty,
                    Type = request.Trail.Type,
                    Traffic = request.Trail.Traffic,
                    Attractions = request.Trail.Attractions,
                    Suitabilities = request.Trail.Suitabilities,
                    Trailhead = new TrailheadLocation
                    {
                        Country = request.Trail.Trailhead.Country,
                        County = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.County : null,
                        Street = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.Street : null,
                        Street2 = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.Street2 : null,
                        City = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.City : null,
                        State = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.State : null,
                        PostalCode = request.Trail.Trailhead.Country == "United States" ? request.Trail.Trailhead.PostalCode : null,
                        Latitude = request.Trail.Trailhead.Latitude,
                        Longitude = request.Trail.Trailhead.Longitude,
                        Altitude = request.Trail.Trailhead.Altitude,
                        InternationalLocation = request.Trail.Trailhead.Country != "United States" ? new InternationalAddress
                        {
                            Address = request.Trail.Trailhead.InternationalLocation.Address
                        } : null
                    },
                    Image = request.Trail.Image,
                    CreatedBy = currentUser.Id,
                    DateCreated = DateTime.UtcNow,
                    IsActive = true
                };

                var succeeded = await trailsService.AddAsync(newTrail);
                if (!succeeded) throw new Exception("Error Saving New Trail.");

                var savedTrail = await trailsService.FindByNameAsync(newTrail.Name);
                return savedTrail;
            }
        }
    }
}