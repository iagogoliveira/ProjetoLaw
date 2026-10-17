using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.OrgStructure;

public sealed record BranchResponse(Guid Id, string Name, string? Cnpj, string? MunicipalRegistration);

public sealed record PracticeAreaResponse(Guid Id, string Name);

public sealed record TeamResponse(Guid Id, string Name, Guid? PracticeAreaId, Guid? BranchId, IReadOnlyList<Guid> MemberIds);

public sealed record CostCenterResponse(Guid Id, string Code, string Name);

public sealed record PartnerResponse(Guid Id, string Name, decimal SharePercentage, Guid? MemberId);

/// <summary>Filial: nome único no escritório e CNPJ opcional (validado pelos dígitos verificadores).</summary>
public sealed record SaveBranchCommand(Guid? Id, string Name, string? Cnpj, string? MunicipalRegistration) : ICommand<BranchResponse>;

public sealed record SavePracticeAreaCommand(Guid? Id, string Name) : ICommand<PracticeAreaResponse>;

/// <summary>Equipe, opcionalmente ligada a uma área e a uma filial do escritório.</summary>
public sealed record SaveTeamCommand(Guid? Id, string Name, Guid? PracticeAreaId, Guid? BranchId) : ICommand<TeamResponse>;

/// <summary>Substitui os membros de uma equipe (base do escopo de dados "equipe").</summary>
public sealed record SetTeamMembersCommand(Guid TeamId, IReadOnlyList<Guid> MemberIds) : ICommand<TeamResponse>;

public sealed record SaveCostCenterCommand(Guid? Id, string Code, string Name) : ICommand<CostCenterResponse>;

/// <summary>Sócio e participação; a soma das participações do escritório não passa de 100%.</summary>
public sealed record SavePartnerCommand(Guid? Id, string Name, decimal SharePercentage, Guid? MemberId) : ICommand<PartnerResponse>;

/// <summary>Tipos de cadastro de estrutura, para a exclusão.</summary>
public enum StructureKind
{
    Branch,
    PracticeArea,
    Team,
    CostCenter,
    Partner,
}

/// <summary>Exclusão lógica; barrada enquanto o item estiver em uso (equipes, escopos de perfis, convites pendentes).</summary>
public sealed record DeleteStructureCommand(StructureKind Kind, Guid Id) : ICommand;

public sealed record ListBranchesQuery : IQuery<IReadOnlyList<BranchResponse>>;

public sealed record ListPracticeAreasQuery : IQuery<IReadOnlyList<PracticeAreaResponse>>;

public sealed record ListTeamsQuery : IQuery<IReadOnlyList<TeamResponse>>;

public sealed record ListCostCentersQuery : IQuery<IReadOnlyList<CostCenterResponse>>;

public sealed record ListPartnersQuery : IQuery<IReadOnlyList<PartnerResponse>>;

internal static class StructureRules
{
    public static IRuleBuilderOptions<T, string> StructureName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("organization.name_required")
            .MaximumLength(StructureEntity.MaxNameLength).WithErrorCode("organization.name_too_long");
}

internal sealed class SaveBranchValidator : AbstractValidator<SaveBranchCommand>
{
    public SaveBranchValidator()
    {
        RuleFor(c => c.Name).StructureName();
        RuleFor(c => c.Cnpj).Must(c => string.IsNullOrWhiteSpace(c) || Cnpj.Create(c).IsSuccess).WithErrorCode(OrganizationErrors.InvalidCnpj.Code);
        RuleFor(c => c.MunicipalRegistration).MaximumLength(30).WithErrorCode("organization.municipal_registration_too_long");
    }
}

internal sealed class SavePracticeAreaValidator : AbstractValidator<SavePracticeAreaCommand>
{
    public SavePracticeAreaValidator() => RuleFor(c => c.Name).StructureName();
}

internal sealed class SaveTeamValidator : AbstractValidator<SaveTeamCommand>
{
    public SaveTeamValidator() => RuleFor(c => c.Name).StructureName();
}

internal sealed class SaveCostCenterValidator : AbstractValidator<SaveCostCenterCommand>
{
    public SaveCostCenterValidator()
    {
        RuleFor(c => c.Name).StructureName();
        RuleFor(c => c.Code).NotEmpty().WithErrorCode("organization.code_required")
            .MaximumLength(CostCenter.MaxCodeLength).WithErrorCode("organization.code_too_long");
    }
}

internal sealed class SavePartnerValidator : AbstractValidator<SavePartnerCommand>
{
    public SavePartnerValidator()
    {
        RuleFor(c => c.Name).StructureName();
        RuleFor(c => c.SharePercentage).Must(s => s is > 0 and <= 100).WithErrorCode(OrganizationErrors.InvalidPartnerShare.Code);
    }
}

internal sealed class SaveBranchHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<SaveBranchCommand, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(SaveBranchCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.Branches.AnyAsync(b => b.Name == name && b.Id != command.Id, cancellationToken))
        {
            return OrganizationErrors.NameTaken;
        }

        var cnpj = string.IsNullOrWhiteSpace(command.Cnpj) ? null : Cnpj.Create(command.Cnpj).Value;
        Branch? branch;
        if (command.Id is { } id)
        {
            branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (branch is null)
            {
                return OrganizationErrors.StructureNotFound;
            }

            branch.Update(name, cnpj, command.MunicipalRegistration);
        }
        else
        {
            branch = Branch.Create(ids.NewId(), name, cnpj, command.MunicipalRegistration);
            db.Branches.Add(branch);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BranchResponse(branch.Id, branch.Name, branch.Cnpj, branch.MunicipalRegistration);
    }
}

internal sealed class SavePracticeAreaHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<SavePracticeAreaCommand, PracticeAreaResponse>
{
    public async Task<Result<PracticeAreaResponse>> Handle(SavePracticeAreaCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.PracticeAreas.AnyAsync(a => a.Name == name && a.Id != command.Id, cancellationToken))
        {
            return OrganizationErrors.NameTaken;
        }

        PracticeArea? area;
        if (command.Id is { } id)
        {
            area = await db.PracticeAreas.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (area is null)
            {
                return OrganizationErrors.StructureNotFound;
            }

            area.Rename(name);
        }
        else
        {
            area = PracticeArea.Create(ids.NewId(), name);
            db.PracticeAreas.Add(area);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PracticeAreaResponse(area.Id, area.Name);
    }
}

internal sealed class SaveTeamHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<SaveTeamCommand, TeamResponse>
{
    public async Task<Result<TeamResponse>> Handle(SaveTeamCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.Teams.AnyAsync(t => t.Name == name && t.Id != command.Id, cancellationToken))
        {
            return OrganizationErrors.NameTaken;
        }

        if ((command.PracticeAreaId is { } areaId && !await db.PracticeAreas.AnyAsync(a => a.Id == areaId, cancellationToken))
            || (command.BranchId is { } branchId && !await db.Branches.AnyAsync(b => b.Id == branchId, cancellationToken)))
        {
            return OrganizationErrors.StructureReferenceNotFound;
        }

        Team? team;
        if (command.Id is { } id)
        {
            team = await db.Teams.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (team is null)
            {
                return OrganizationErrors.StructureNotFound;
            }

            team.Update(name, command.PracticeAreaId, command.BranchId);
        }
        else
        {
            team = Team.Create(ids.NewId(), name, command.PracticeAreaId, command.BranchId);
            db.Teams.Add(team);
        }

        await db.SaveChangesAsync(cancellationToken);
        var members = await db.TeamMembers.Where(m => m.TeamId == team.Id).Select(m => m.MemberId).ToListAsync(cancellationToken);
        return new TeamResponse(team.Id, team.Name, team.PracticeAreaId, team.BranchId, members);
    }
}

internal sealed class SetTeamMembersHandler(IOrganizationDbContext db, AccessChanges changes, IIdGenerator ids) : ICommandHandler<SetTeamMembersCommand, TeamResponse>
{
    public async Task<Result<TeamResponse>> Handle(SetTeamMembersCommand command, CancellationToken cancellationToken)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TeamId, cancellationToken);
        if (team is null)
        {
            return OrganizationErrors.StructureNotFound;
        }

        var wanted = command.MemberIds.Distinct().ToList();
        if (await db.Members.CountAsync(m => wanted.Contains(m.Id), cancellationToken) != wanted.Count)
        {
            return OrganizationErrors.MemberNotFound;
        }

        var current = await db.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(current.Where(m => !wanted.Contains(m.MemberId)));
        db.TeamMembers.AddRange(wanted.Where(id => !current.Exists(m => m.MemberId == id)).Select(id => TeamMember.Add(ids.NewId(), team.Id, id)));

        // Quem tem escopo "equipe" passa a enxergar (ou deixa de enxergar) a carteira dos colegas.
        await changes.SaveAsync(null, cancellationToken);
        return new TeamResponse(team.Id, team.Name, team.PracticeAreaId, team.BranchId, wanted);
    }
}

internal sealed class SaveCostCenterHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<SaveCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(SaveCostCenterCommand command, CancellationToken cancellationToken)
    {
        var code = CostCenter.NormalizeCode(command.Code);
        if (await db.CostCenters.AnyAsync(c => c.Code == code && c.Id != command.Id, cancellationToken))
        {
            return OrganizationErrors.CodeTaken;
        }

        CostCenter? costCenter;
        if (command.Id is { } id)
        {
            costCenter = await db.CostCenters.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (costCenter is null)
            {
                return OrganizationErrors.StructureNotFound;
            }

            costCenter.Update(code, command.Name);
        }
        else
        {
            costCenter = CostCenter.Create(ids.NewId(), code, command.Name);
            db.CostCenters.Add(costCenter);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CostCenterResponse(costCenter.Id, costCenter.Code, costCenter.Name);
    }
}

internal sealed class SavePartnerHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<SavePartnerCommand, PartnerResponse>
{
    public async Task<Result<PartnerResponse>> Handle(SavePartnerCommand command, CancellationToken cancellationToken)
    {
        if (command.MemberId is { } memberId && !await db.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            return OrganizationErrors.MemberNotFound;
        }

        var others = await db.Partners.Where(p => p.Id != command.Id).SumAsync(p => p.SharePercentage, cancellationToken);
        if (others + command.SharePercentage > 100)
        {
            return OrganizationErrors.PartnerSharesExceedTotal;
        }

        Partner? partner;
        if (command.Id is { } id)
        {
            partner = await db.Partners.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (partner is null)
            {
                return OrganizationErrors.StructureNotFound;
            }

            var updated = partner.Update(command.Name, command.SharePercentage, command.MemberId);
            if (updated.IsFailure)
            {
                return updated.Error;
            }
        }
        else
        {
            var created = Partner.Create(ids.NewId(), command.Name, command.SharePercentage, command.MemberId);
            if (created.IsFailure)
            {
                return created.Error;
            }

            partner = created.Value;
            db.Partners.Add(partner);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PartnerResponse(partner.Id, partner.Name, partner.SharePercentage, partner.MemberId);
    }
}

internal sealed class DeleteStructureHandler(IOrganizationDbContext db, RoleAssignments rules) : ICommandHandler<DeleteStructureCommand, Unit>
{
    public Task<Result<Unit>> Handle(DeleteStructureCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        return command.Kind switch
        {
            StructureKind.Branch => DeleteAsync(db.Branches, id, async () => await db.Teams.AnyAsync(t => t.BranchId == id, cancellationToken) || await rules.ScopeInUseAsync(DataScope.Branch, id, cancellationToken), cancellationToken),
            StructureKind.PracticeArea => DeleteAsync(db.PracticeAreas, id, async () => await db.Teams.AnyAsync(t => t.PracticeAreaId == id, cancellationToken) || await rules.ScopeInUseAsync(DataScope.PracticeArea, id, cancellationToken), cancellationToken),
            StructureKind.Team => DeleteTeamAsync(id, cancellationToken),
            StructureKind.CostCenter => DeleteAsync(db.CostCenters, id, () => Task.FromResult(false), cancellationToken),
            _ => DeleteAsync(db.Partners, id, () => Task.FromResult(false), cancellationToken),
        };
    }

    private async Task<Result<Unit>> DeleteTeamAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await db.Teams.AnyAsync(t => t.Id == id, cancellationToken) && !await rules.ScopeInUseAsync(DataScope.Team, id, cancellationToken))
        {
            db.TeamMembers.RemoveRange(await db.TeamMembers.Where(m => m.TeamId == id).ToListAsync(cancellationToken));
        }

        return await DeleteAsync(db.Teams, id, () => rules.ScopeInUseAsync(DataScope.Team, id, cancellationToken), cancellationToken);
    }

    /// <summary>Exclusão lógica (<c>ISoftDeletable</c>): o interceptor transforma o <c>Remove</c> em <c>is_deleted</c>.</summary>
    private async Task<Result<Unit>> DeleteAsync<T>(DbSet<T> set, Guid id, Func<Task<bool>> inUse, CancellationToken cancellationToken)
        where T : StructureEntity
    {
        var entity = await set.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return OrganizationErrors.StructureNotFound;
        }

        if (await inUse())
        {
            return OrganizationErrors.StructureInUse;
        }

        set.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal sealed class ListBranchesHandler(IOrganizationDbContext db) : IQueryHandler<ListBranchesQuery, IReadOnlyList<BranchResponse>>
{
    public async Task<Result<IReadOnlyList<BranchResponse>>> Handle(ListBranchesQuery query, CancellationToken cancellationToken) =>
        await db.Branches.AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new BranchResponse(b.Id, b.Name, b.Cnpj, b.MunicipalRegistration))
            .ToListAsync(cancellationToken);
}

internal sealed class ListPracticeAreasHandler(IOrganizationDbContext db) : IQueryHandler<ListPracticeAreasQuery, IReadOnlyList<PracticeAreaResponse>>
{
    public async Task<Result<IReadOnlyList<PracticeAreaResponse>>> Handle(ListPracticeAreasQuery query, CancellationToken cancellationToken) =>
        await db.PracticeAreas.AsNoTracking().OrderBy(a => a.Name)
            .Select(a => new PracticeAreaResponse(a.Id, a.Name))
            .ToListAsync(cancellationToken);
}

internal sealed class ListTeamsHandler(IOrganizationDbContext db) : IQueryHandler<ListTeamsQuery, IReadOnlyList<TeamResponse>>
{
    public async Task<Result<IReadOnlyList<TeamResponse>>> Handle(ListTeamsQuery query, CancellationToken cancellationToken)
    {
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var members = (await db.TeamMembers.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(m => m.TeamId, m => m.MemberId);
        return teams.Select(t => new TeamResponse(t.Id, t.Name, t.PracticeAreaId, t.BranchId, [.. members[t.Id]])).ToList();
    }
}

internal sealed class ListCostCentersHandler(IOrganizationDbContext db) : IQueryHandler<ListCostCentersQuery, IReadOnlyList<CostCenterResponse>>
{
    public async Task<Result<IReadOnlyList<CostCenterResponse>>> Handle(ListCostCentersQuery query, CancellationToken cancellationToken) =>
        await db.CostCenters.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new CostCenterResponse(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);
}

internal sealed class ListPartnersHandler(IOrganizationDbContext db) : IQueryHandler<ListPartnersQuery, IReadOnlyList<PartnerResponse>>
{
    public async Task<Result<IReadOnlyList<PartnerResponse>>> Handle(ListPartnersQuery query, CancellationToken cancellationToken) =>
        await db.Partners.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new PartnerResponse(p.Id, p.Name, p.SharePercentage, p.MemberId))
            .ToListAsync(cancellationToken);
}
