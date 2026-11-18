const COMMENT =
  "This PR or issue was automatically closed because prohibited words or phrases were found in its title. " +
  "If this was a mistake, update the title and reopen it.";

function normalize(value) {
  return value
    .normalize("NFKC")
    .toLocaleLowerCase("en")
    .replace(/\s+/g, " ")
    .trim();
}

module.exports = async function moderateTitles({ github, context, core, terms }) {
  const bannedTerms = terms
    .split(/\r?\n/)
    .map((line) => line.replace(/^\uFEFF/, "").trim())
    .filter((line) => line && !line.startsWith("#"))
    .map(normalize)
    .filter(Boolean);

  if (bannedTerms.length === 0) {
    core.info("No banned terms configured.");
    return;
  }

  const { owner, repo } = context.repo;
  const openItems = await github.paginate(github.rest.issues.listForRepo, {
    owner,
    repo,
    state: "open",
    per_page: 100,
  });

  for (const item of openItems) {
    const title = normalize(item.title || "");
    const matchedTerm = bannedTerms.find((term) => title.includes(term));

    if (!matchedTerm) continue;

    core.info(`Closing #${item.number}: matched "${matchedTerm}" in title`);

    await github.rest.issues.createComment({
      owner,
      repo,
      issue_number: item.number,
      body: `${COMMENT}\n\nMatched term: \`${matchedTerm}\``,
    });

    await github.rest.issues.update({
      owner,
      repo,
      issue_number: item.number,
      state: "closed",
      state_reason: "not_planned",
    });
  }
};
