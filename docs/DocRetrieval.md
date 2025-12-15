# Doc acquisition status for DocRev25.pdf and APIDocRev28.pdf

Attempts were made to locate the requested protocol documents using public search endpoints from within the build container. Searches against DuckDuckGo returned HTTP 202 responses with no result links, and the `duckduckgo_search` Python helper failed due to TLS certificate validation errors when proxying through Bing. Because the source URLs for DocRev25.pdf and APIDocRev28.pdf could not be discovered and the search endpoints refused connections, the PDFs could not be downloaded for analysis.

Steps attempted:
1. Queried DuckDuckGo's HTML endpoint via `requests`; received status 202 with an empty result set.
2. Installed `duckduckgo-search` as an alternative query helper; queries raised certificate failures when the backend tried to reach Bing.

Please provide direct download URLs or host the PDFs within the repository so that the command-gap analysis can be completed.
