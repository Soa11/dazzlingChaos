// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Convenience methods for working with collections of <see cref="ConditionalProject"/>.
    /// </summary>
    internal static class ConditionalProjectCollectionExtensions
    {
        [return: MaybeNull]
        internal static ConditionalProject GetActive(this IReadOnlyList<ConditionalProject> projects, bool? isDevelopmentMode = null)
        {
            if (projects.Count is 0)
            {
                return null;
            }

            return projects.FirstOrDefault(x => x.AreConditionsMet(isDevelopmentMode, false))
                   ?? projects.FirstOrDefault(x => !x.HasConditions);
        }

        [return: MaybeNull]
        internal static ProjectInfo GetActiveProject(this IReadOnlyList<ConditionalProject> projects, bool? isDevelopmentMode = null)
            => projects.GetActive(isDevelopmentMode)?.Project;

        [return: MaybeNull]
        internal static string GetActiveProjectId(this IReadOnlyList<ConditionalProject> projects, bool? isDevelopmentMode = null)
            => projects.GetActive(isDevelopmentMode)?.Project.id;

        [return: MaybeNull]
        internal static ConditionalProject GetById(this IReadOnlyList<ConditionalProject> projects, string projectId)
        {
            foreach (var conditionalProject in projects)
            {
                if (string.Equals(conditionalProject.Project.id, projectId))
                {
                    return conditionalProject;
                }
            }

            return null;
        }

        [return: MaybeNull]
        internal static ProjectInfo GetProjectById(this IReadOnlyList<ConditionalProject> projects, string projectId)
            => projects.GetById(projectId)?.Project;

        internal static IEnumerable<ConditionalProject> GetValidAndDistinct(this IReadOnlyList<ConditionalProject> projects)
        {
            if (projects.Count is 0)
            {
                yield break;
            }

            var addedIds = new HashSet<string>(projects.Count);
            for (var i = 0; i < projects.Count; i++)
            {
                var conditionalProject = projects[i];
                if (conditionalProject.Project is { id: { Length: > 0 } } project && addedIds.Add(project.id))
                {
                    yield return conditionalProject;
                }
            }
        }

        internal static IEnumerable<ProjectInfo> GetValidAndDistinctProjects(this IReadOnlyList<ConditionalProject> projects)
            => projects.GetValidAndDistinct().Select(x => x.Project);

        internal static IEnumerable<string> GetValidAndDistinctProjectIds(this IReadOnlyList<ConditionalProject> projects)
            => projects.GetValidAndDistinct().Select(x => x.Project.id);

        internal static ConditionalProject[] GetByIds(this IReadOnlyList<ConditionalProject> projects, string[] projectsIds)
            => projectsIds.Select(projects.GetById).ToArray();

        internal static ProjectInfo[] GetProjects(this IReadOnlyList<ConditionalProject> projects)
            => projects.Select(x => x.Project).ToArray();
    }
}
